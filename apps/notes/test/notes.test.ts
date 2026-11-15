import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { migrate, type Database } from '../src/db.js';
import { NotesError } from '../src/errors.js';
import { createNotes, type Notes } from '../src/notes.js';
import { createTestDatabase, createTestUser } from './helpers.js';

let db: Database;
let notes: Notes;

beforeEach(async () => {
  db = await createTestDatabase();
  notes = createNotes(db, await createTestUser(db));
});
afterEach(() => db.close());

const entry = {
  project: '預約系統',
  module: '實驗設備預約',
  title: '黑名單擋預約',
  what_changed: '在送出預約前檢查黑名單',
  why: '違規使用者不應該能預約設備',
};

describe('migrate', () => {
  it('再跑一次不會重複套用', async () => {
    expect(await migrate(db)).toEqual([]);
  });
});

describe('add_entry', () => {
  it('自動建立專案和模組，第二次沿用且不分大小寫', async () => {
    const first = await notes.addEntry({ ...entry, project: 'My App', module: 'Login' });
    expect(first).toMatchObject({ project_created: true, module_created: true });
    const second = await notes.addEntry({ ...entry, project: 'my app', module: 'LOGIN' });
    expect(second).toMatchObject({ project_created: false, module_created: false });
  });

  it('沒填測試狀態視為沒測過，日期預設今天', async () => {
    const { id, happened_on } = await notes.addEntry(entry);
    expect(happened_on).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    const { entries } = await notes.searchEntries({});
    expect(entries[0]).toMatchObject({ id, test_status: 'not_tested', happened_on });
  });

  it('含機密時整筆不寫入，錯誤訊息不回顯內容', async () => {
    const cases = [
      'postgres://admin:hunter2@10.0.0.1/db',
      'Data Source=ORCL;User Id=sa;Password=abc',
      'password=Sup3rSecret!',
      '密碼：abc12345',
      'ghp_abcdefghijklmnopqrstuvwxyz123456',
      '聯絡 someone@example.com',
    ];
    for (const value of cases) {
      const error = await notes.addEntry({ ...entry, data_notes: value }).catch((e: unknown) => e);
      expect(error, value).toBeInstanceOf(NotesError);
      expect((error as Error).message).toContain('data_notes');
      expect((error as Error).message).not.toContain(value);
    }
    expect((await notes.searchEntries({})).count).toBe(0);
  });

  it('一般敘述不會被誤擋', async () => {
    const values = [
      'password: required，長度至少 8',
      '密碼：必須包含英數',
      'WHERE password = @p_password',
      '料號 607-7C37-010，序號 A123456789',
      'https://github.com/org/repo/pull/12',
    ];
    for (const value of values) {
      await expect(notes.addEntry({ ...entry, logic_notes: value }), value).resolves.toBeDefined();
    }
  });

  it('連結存在的觀念卡，查不到的回報出來', async () => {
    await notes.saveConcept({ term: '料號', plain_explanation: '零件的編號', confidence: 'general_definition' });
    const result = await notes.addEntry({ ...entry, concept_terms: ['料號', '不存在'] });
    expect(result.linked_concepts).toEqual(['料號']);
    expect(result.unresolved_concept_terms).toEqual(['不存在']);
  });
});

describe('update_entry / delete_entry', () => {
  it('只改有傳的欄位，選填欄位傳空字串會清空', async () => {
    const { id } = await notes.addEntry({ ...entry, logic_notes: '舊的邏輯', test_notes: '保留' });
    const result = await notes.updateEntry({ id, why: '新的原因', logic_notes: '', test_status: 'manual' });
    expect(result.updated_fields.sort()).toEqual(['logic_notes', 'test_status', 'why']);

    const [row] = (await notes.searchEntries({})).entries;
    expect(row).toMatchObject({
      title: entry.title,
      why: '新的原因',
      logic_notes: null,
      test_notes: '保留',
      test_status: 'manual',
    });
  });

  it('必填欄位不能清空，沒傳任何欄位或 id 不存在都報錯', async () => {
    const { id } = await notes.addEntry(entry);
    await expect(notes.updateEntry({ id, title: '  ' })).rejects.toThrow(NotesError);
    await expect(notes.updateEntry({ id })).rejects.toThrow('沒有要修改的欄位');
    await expect(notes.updateEntry({ id: 999, why: 'x' })).rejects.toThrow('找不到改動紀錄 id：999');
  });

  it('修改時也擋機密', async () => {
    const { id } = await notes.addEntry(entry);
    await expect(notes.updateEntry({ id, data_notes: 'password=Sup3rSecret!' })).rejects.toThrow(NotesError);
  });

  it('刪除紀錄和觀念關聯，觀念卡本身保留', async () => {
    await notes.saveConcept({ term: '黑名單', plain_explanation: '禁止名單', confidence: 'general_definition' });
    const { id } = await notes.addEntry({ ...entry, concept_terms: ['黑名單'] });
    expect(await notes.deleteEntry({ id })).toEqual({ deleted: { id, title: entry.title } });

    expect((await notes.searchEntries({})).count).toBe(0);
    const { concepts } = await notes.searchConcepts({ query: '黑名單' });
    expect(concepts[0]).toMatchObject({ term: '黑名單', entry_ids: [] });
    await expect(notes.deleteEntry({ id })).rejects.toThrow(`找不到改動紀錄 id：${id}`);
  });
});

describe('依 id 查詢、刪除觀念卡', () => {
  it('依 id 只取一筆改動紀錄或觀念卡', async () => {
    const first = await notes.addEntry(entry);
    await notes.addEntry({ ...entry, title: '另一筆' });
    expect((await notes.searchEntries({ id: first.id })).entries).toMatchObject([{ id: first.id, title: entry.title }]);

    const concept = await notes.saveConcept({ term: '料號', plain_explanation: '零件的編號', confidence: 'general_definition' });
    await notes.saveConcept({ term: '工單', plain_explanation: '生產指令', confidence: 'general_definition' });
    expect((await notes.searchConcepts({ id: concept.id })).concepts).toMatchObject([{ term: '料號' }]);
  });

  it('刪除觀念卡和它的關聯，改動紀錄和其他觀念卡保留', async () => {
    await notes.saveConcept({ term: '工單', plain_explanation: '生產指令', confidence: 'general_definition' });
    await notes.saveConcept({
      term: '料號',
      plain_explanation: '零件的編號',
      confidence: 'general_definition',
      related_terms: ['工單'],
    });
    const { id } = await notes.addEntry({ ...entry, concept_terms: ['料號'] });

    expect(await notes.deleteConcept({ term: '料號' })).toMatchObject({ deleted: { term: '料號' } });
    const [kept] = (await notes.searchEntries({ id })).entries as Array<{ concepts: string[] }>;
    expect(kept!.concepts).toEqual([]);
    expect((await notes.searchConcepts({ query: '工單' })).concepts).toMatchObject([{ term: '工單', related: [] }]);
    await expect(notes.deleteConcept({ term: '料號' })).rejects.toThrow('找不到觀念卡「料號」');
  });
});

describe('search_entries', () => {
  beforeEach(async () => {
    await notes.addEntry(entry);
    await notes.addEntry({ project: '私人專案', title: '料號編碼', what_changed: '調整 BTO 料號規則', why: '對應新機種', test_status: 'manual' });
  });

  it('中文可以搜到句子中間的片段，包含一兩個字的詞', async () => {
    const twoChars = await notes.searchEntries({ query: '料號' });
    expect(twoChars.entries).toMatchObject([{ title: '料號編碼' }]);
    const oneChar = await notes.searchEntries({ query: '單' });
    expect(oneChar.entries).toMatchObject([{ title: '黑名單擋預約' }]);
  });

  it('多個關鍵字全部都要符合', async () => {
    expect((await notes.searchEntries({ query: '料號 新機種' })).count).toBe(1);
    expect((await notes.searchEntries({ query: '料號 黑名單' })).count).toBe(0);
  });

  it('% 和 _ 當成一般文字', async () => {
    expect((await notes.searchEntries({ query: '%' })).count).toBe(0);
    expect((await notes.searchEntries({ query: '_' })).count).toBe(0);
  });

  it('可以依專案、模組和測試狀態篩選，也會限制筆數', async () => {
    expect((await notes.searchEntries({ project: '預約系統' })).count).toBe(1);
    expect((await notes.searchEntries({ module: '實驗設備預約' })).count).toBe(1);
    expect((await notes.searchEntries({ test_status: 'manual' })).count).toBe(1);
    expect((await notes.searchEntries({ limit: 1 })).count).toBe(1);
  });
});

describe('rules', () => {
  const rule = { title: '預約上限', body: '每人同時最多 3 筆', confidence: 'code_inferred' as const };
  const scope = { project: '預約系統', module: '實驗設備預約' };

  it('已確認一定要有確認者，未確認不能填確認者', async () => {
    await expect(notes.updateProjectRules({ ...scope, rules: [{ ...rule, confidence: 'colleague_confirmed' }] })).rejects.toThrow(/confirmed_by/);
    await expect(notes.updateProjectRules({ ...scope, rules: [{ ...rule, confirmed_by: '小明' }] })).rejects.toThrow(/confirmed_by/);
  });

  it('改內容且降回推測時，舊的確認者會消失', async () => {
    await notes.updateProjectRules({ ...scope, rules: [{ ...rule, confidence: 'colleague_confirmed', confirmed_by: '小明' }] });
    const saved = await notes.updateProjectRules({ ...scope, rules: [{ ...rule, body: '每人最多 5 筆' }] });
    expect(saved.saved).toEqual([{ title: '預約上限', created: false }]);
    const { rules } = await notes.getProjectRules(scope);
    expect(rules).toMatchObject([{ body: '每人最多 5 筆', confidence: 'code_inferred', confirmed_by: null }]);
  });

  it('標記不再適用後查不到，再存一次會恢復', async () => {
    await notes.updateProjectRules({ ...scope, rules: [rule] });
    const retired = await notes.updateProjectRules({ ...scope, retire_titles: ['預約上限', '沒有這條'] });
    expect(retired).toMatchObject({ retired: ['預約上限'], retire_not_found: ['沒有這條'] });
    expect((await notes.getProjectRules(scope)).count).toBe(0);
    await notes.updateProjectRules({ ...scope, rules: [rule] });
    expect((await notes.getProjectRules(scope)).count).toBe(1);
  });

  it('查不到專案時列出現有專案', async () => {
    await notes.addEntry(entry);
    await expect(notes.getProjectRules({ project: '預約系統2' })).rejects.toThrow(/現有專案：預約系統/);
  });

  it('沒傳任何變更就報錯', async () => {
    await expect(notes.updateProjectRules(scope)).rejects.toBeInstanceOf(NotesError);
  });
});

describe('stats', () => {
  it('統計筆數和可信度，不算已停用的規則', async () => {
    const scope = { project: '預約系統', module: '實驗設備預約' };
    await notes.addEntry(entry);
    await notes.saveConcept({ term: '料號', plain_explanation: 'x', confidence: 'general_definition', open_questions: '?' });
    await notes.saveConcept({ term: 'BOM', plain_explanation: 'y', confidence: 'colleague_confirmed', confirmed_by: '小明' });
    await notes.updateProjectRules({
      ...scope,
      rules: [
        { title: '甲', body: 'a', confidence: 'code_inferred' },
        { title: '乙', body: 'b', confidence: 'code_inferred' },
      ],
    });
    await notes.updateProjectRules({ ...scope, retire_titles: ['乙'] });

    expect(await notes.getStats()).toEqual({
      entries: 1,
      articles: 0,
      concepts: 2,
      rules: 1,
      open_questions: 1,
      general_definition: 1,
      code_inferred: 1,
      colleague_confirmed: 1,
    });
  });
});

describe('articles', () => {
  const article = { project: 'global-system', title: 'ERP 怎麼串起來', body: '訂單 → 工單 → 出貨', confidence: 'code_inferred' as const };

  it('自動建立專案；同專案同標題是同一篇，不分大小寫', async () => {
    const first = await notes.saveArticle(article);
    expect(first).toMatchObject({ created: true, project_created: true });
    const second = await notes.saveArticle({ ...article, title: 'erp 怎麼串起來', body: '改過的內文' });
    expect(second).toMatchObject({ id: first.id, created: false, project_created: false });
    expect((await notes.searchArticles({})).articles).toMatchObject([{ title: 'erp 怎麼串起來', body: '改過的內文' }]);

    // 不同專案可以有同標題的筆記
    const other = await notes.saveArticle({ ...article, project: '別的專案' });
    expect(other.id).not.toBe(first.id);
  });

  it('沒傳的選填欄位保留，空字串才清空；觀念卡關聯有傳才整組換掉', async () => {
    await notes.saveConcept({ term: '料號', plain_explanation: 'x', confidence: 'general_definition' });
    await notes.saveConcept({ term: 'BOM', plain_explanation: 'y', confidence: 'general_definition' });
    const saved = await notes.saveArticle({ ...article, module: '料號', open_questions: 'G 料號是什麼？', concept_terms: ['料號', '查不到'] });
    expect(saved).toMatchObject({ module_created: true, linked_concepts: ['料號'], unresolved_concept_terms: ['查不到'] });

    await notes.saveArticle(article);
    expect((await notes.searchArticles({})).articles).toMatchObject([{ module: '料號', open_questions: 'G 料號是什麼？', concepts: ['料號'] }]);

    await notes.saveArticle({ ...article, module: '', open_questions: '', concept_terms: ['BOM'] });
    expect((await notes.searchArticles({})).articles).toMatchObject([{ module: null, open_questions: null, concepts: ['BOM'] }]);
  });

  it('擋機密、太長的內文和沒有確認者的「已確認」', async () => {
    await expect(notes.saveArticle({ ...article, body: 'password=hunter2hunter2' })).rejects.toThrow(NotesError);
    await expect(notes.saveArticle({ ...article, body: 'x'.repeat(20001) })).rejects.toThrow(/拆成幾篇/);
    await expect(notes.saveArticle({ ...article, confidence: 'colleague_confirmed' })).rejects.toThrow(/confirmed_by/);
    expect((await notes.searchArticles({})).count).toBe(0);
  });

  it('可以依關鍵字、專案、待釐清篩選；刪除只解除觀念卡關聯', async () => {
    await notes.saveConcept({ term: '料號', plain_explanation: 'x', confidence: 'general_definition' });
    const { id } = await notes.saveArticle({ ...article, concept_terms: ['料號'], open_questions: '還不懂' });
    await notes.saveArticle({ project: '預約系統', title: '預約流程', body: '先查黑名單', confidence: 'general_definition' });

    expect((await notes.searchArticles({ query: '工單' })).articles).toMatchObject([{ id }]);
    expect((await notes.searchArticles({ project: '預約系統' })).articles).toMatchObject([{ title: '預約流程' }]);
    expect((await notes.searchArticles({ has_open_questions: true })).articles).toMatchObject([{ id }]);
    expect((await notes.searchArticles({ id })).count).toBe(1);

    await notes.deleteArticle({ id });
    expect((await notes.searchArticles({ id })).count).toBe(0);
    expect((await notes.searchConcepts({ query: '料號' })).count).toBe(1);
    await expect(notes.deleteArticle({ id })).rejects.toThrow(/找不到筆記/);
  });

  it('刪掉觀念卡時一併解除和筆記的關聯', async () => {
    await notes.saveConcept({ term: '料號', plain_explanation: 'x', confidence: 'general_definition' });
    await notes.saveArticle({ ...article, concept_terms: ['料號'] });
    await notes.deleteConcept({ term: '料號' });
    expect((await notes.searchArticles({})).articles).toMatchObject([{ concepts: [] }]);
  });

  it('專案清單和統計會算進一般筆記', async () => {
    await notes.saveArticle(article);
    expect((await notes.listProjects()).projects).toContainEqual(expect.objectContaining({ name: 'global-system', article_count: 1 }));
    expect(await notes.getStats()).toMatchObject({ articles: 1, code_inferred: 1 });
  });

  it('別人看不到也刪不到自己的筆記', async () => {
    const { id } = await notes.saveArticle(article);
    const other = createNotes(db, await createTestUser(db, '別人'));
    expect((await other.searchArticles({})).count).toBe(0);
    await expect(other.deleteArticle({ id })).rejects.toThrow(/找不到筆記/);
  });
});

describe('concepts', () => {
  const concept = { term: '料號', plain_explanation: '零件的編號', confidence: 'general_definition' as const };

  it('沒傳的選填欄位保留，空字串才清空', async () => {
    await notes.saveConcept({ ...concept, example: '607-7C37-010', open_questions: '編碼規則？' });
    await notes.saveConcept({ ...concept, plain_explanation: '料件的唯一編號', open_questions: '' });
    const { concepts } = await notes.searchConcepts({ query: '料號' });
    expect(concepts).toMatchObject([{ plain_explanation: '料件的唯一編號', example: '607-7C37-010', open_questions: null }]);
  });

  it('相關觀念是雙向的，重複存不會多一筆', async () => {
    await notes.saveConcept({ term: 'BOM', plain_explanation: '物料清單', confidence: 'general_definition' });
    const saved = await notes.saveConcept({ ...concept, related_terms: ['BOM', '料號', '還沒建'] });
    expect(saved).toMatchObject({ related: ['BOM'], unresolved_related_terms: ['還沒建'] });
    await notes.saveConcept({ ...concept, related_terms: ['BOM'] });
    await notes.saveConcept({ term: 'BOM', plain_explanation: '物料清單', confidence: 'general_definition', related_terms: ['料號'] });
    const { concepts } = await notes.searchConcepts({ query: 'BOM' });
    expect(concepts.find((c) => (c as { term: string }).term === 'BOM')).toMatchObject({ related: ['料號'] });
    const relations = await db.query('SELECT * FROM concept_relations');
    expect(relations.rows).toHaveLength(1);
  });

  it('關聯改動紀錄；id 不存在時整筆不寫入', async () => {
    const { id } = await notes.addEntry(entry);
    await notes.saveConcept({ ...concept, link_entry_ids: [id] });
    expect((await notes.searchConcepts({})).concepts).toMatchObject([{ entry_ids: [id] }]);
    expect((await notes.searchEntries({})).entries).toMatchObject([{ concepts: ['料號'] }]);

    await expect(notes.saveConcept({ term: '新觀念', plain_explanation: 'x', confidence: 'code_inferred', link_entry_ids: [9999] })).rejects.toThrow(/9999/);
    expect((await notes.searchConcepts({ query: '新觀念' })).count).toBe(0);
  });

  it('可以篩出待確認的卡片', async () => {
    await notes.saveConcept({ ...concept, open_questions: '編碼規則？' });
    await notes.saveConcept({ term: 'BOM', plain_explanation: '物料清單', confidence: 'code_inferred' });
    expect((await notes.searchConcepts({ has_open_questions: true })).concepts).toMatchObject([{ term: '料號' }]);
    expect((await notes.searchConcepts({ confidence: 'code_inferred' })).concepts).toMatchObject([{ term: 'BOM' }]);
  });

  it('可以依專案篩：指定了所屬專案，或連到該專案的改動紀錄', async () => {
    const { id } = await notes.addEntry(entry);
    await notes.updateProjectRules({ project: '只有觀念', module: '模組', rules: [{ title: '規則', body: '內容', confidence: 'code_inferred' }] });
    await notes.saveConcept({ ...concept, project: '只有觀念' });
    await notes.saveConcept({ term: 'BOM', plain_explanation: '物料清單', confidence: 'general_definition', link_entry_ids: [id] });
    await notes.saveConcept({ term: '沒歸屬', plain_explanation: 'x', confidence: 'general_definition' });

    expect((await notes.searchConcepts({ project: '只有觀念' })).concepts).toMatchObject([{ term: '料號' }]);
    expect((await notes.searchConcepts({ project: '預約系統' })).concepts).toMatchObject([{ term: 'BOM' }]);
    expect((await notes.searchConcepts({ project: '不存在' })).count).toBe(0);
  });

  it('專案清單也算觀念卡和規則，只有觀念卡的專案不會被漏掉', async () => {
    const { id } = await notes.addEntry(entry);
    await notes.updateProjectRules({ project: '只有觀念', module: '模組', rules: [{ title: '規則', body: '內容', confidence: 'code_inferred' }] });
    await notes.saveConcept({ ...concept, project: '只有觀念' });
    await notes.saveConcept({ term: 'BOM', plain_explanation: '物料清單', confidence: 'general_definition', link_entry_ids: [id] });

    const { projects } = await notes.listProjects();
    expect(projects).toContainEqual(expect.objectContaining({ name: '只有觀念', entry_count: 0, concept_count: 1, rule_count: 1 }));
    expect(projects).toContainEqual(expect.objectContaining({ name: '預約系統', entry_count: 1, concept_count: 1, rule_count: 0 }));
  });

  it('資料庫本身也擋得住沒有確認者的「已確認」', async () => {
    await expect(
      db.query(`INSERT INTO concepts (term, plain_explanation, confidence) VALUES ('x', 'y', 'colleague_confirmed')`),
    ).rejects.toThrow();
  });
});
