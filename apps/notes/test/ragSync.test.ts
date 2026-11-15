import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { Database } from '../src/db.js';
import { buildMcpServer } from '../src/mcp.js';
import { createNotes, type Notes } from '../src/notes.js';
import {
  applyPlan,
  buildPlan,
  loadNoteDocs,
  loadNoteDocsViaMcp,
  markerFor,
  noteKey,
  notesToolCaller,
  parseMarker,
  resolveCategories,
  type NoteDoc,
  type QuestionPayload,
  type RagClient,
  type RemoteQuestion,
} from '../src/ragSync.js';
import { createTestDatabase, createTestUser } from './helpers.js';

let db: Database;
let notes: Notes;
let userId: number;

beforeEach(async () => {
  db = await createTestDatabase();
  userId = await createTestUser(db);
  notes = createNotes(db, userId);
});
afterEach(() => db.close());

const itemIds = { entry: 11, concept: 12, rule: 13, article: 14 };

/** 記在記憶體的假 RAG，行為照 .NET API：新增不回傳 id，分類是共用字典。 */
function fakeRag(initial: RemoteQuestion[] = []) {
  const state = {
    types: [] as Array<{ id: number; nameZh: string }>,
    items: [] as Array<{ id: number; qaTypeID: number; nameZh: string }>,
    questions: [...initial],
    nextId: 100,
  };
  const client: RagClient = {
    listTypes: async () => state.types,
    createType: async (nameZh) => {
      state.types.push({ id: state.nextId++, nameZh });
    },
    listItems: async () => state.items,
    createItem: async (qaTypeID, nameZh) => {
      state.items.push({ id: state.nextId++, qaTypeID, nameZh });
    },
    listQuestions: async () => state.questions,
    createQuestion: async (payload: QuestionPayload) => {
      state.questions.push({ id: state.nextId++, ...payload });
    },
    updateQuestion: async (payload) => {
      const index = state.questions.findIndex((q) => q.id === payload.id);
      state.questions[index] = { ...payload };
    },
    deleteQuestion: async (id) => {
      state.questions = state.questions.filter((q) => q.id !== id);
    },
  };
  return { client, state };
}

async function seed() {
  await notes.saveConcept({ term: '黑名單', plain_explanation: '被禁止使用的名單', confidence: 'code_inferred' });
  await notes.addEntry({
    project: '預約系統',
    module: '實驗設備預約',
    title: '黑名單擋預約',
    what_changed: '在送出預約前檢查黑名單',
    why: '違規使用者不應該能預約設備',
    concept_terms: ['黑名單'],
  });
  await notes.addEntry({ project: '部落格', title: '改首頁', what_changed: '換排版', why: '比較好讀' });
  await notes.updateProjectRules({
    project: '預約系統',
    module: '實驗設備預約',
    rules: [
      { title: '一次只能預約一台', body: '同時段只能有一筆預約', confidence: 'colleague_confirmed', confirmed_by: '小王' },
      { title: '舊規則', body: '已不適用', confidence: 'general_definition' },
    ],
  });
  await notes.updateProjectRules({ project: '預約系統', module: '實驗設備預約', retire_titles: ['舊規則'] });
  await notes.saveArticle({
    project: '預約系統',
    title: '預約流程怎麼串',
    body: '## 先查黑名單\n再檢查時段衝突',
    confidence: 'code_inferred',
    open_questions: '取消後多久能再約？',
    concept_terms: ['黑名單'],
  });
}

describe('loadNoteDocs', () => {
  it('問題只放標題和範圍，答案放整篇筆記並以來源標記結尾', async () => {
    await seed();
    const docs = await loadNoteDocs(db, userId);
    const entry = docs.find((d) => d.kind === 'entry' && d.title === '黑名單擋預約')!;
    expect(entry.question).toBe('黑名單擋預約（預約系統／實驗設備預約）');
    expect(entry.answer).toContain('### 為什麼\n違規使用者不應該能預約設備');
    expect(entry.answer).toContain('相關觀念：黑名單');
    expect(parseMarker(entry.answer)).toEqual({ kind: 'entry', ref: entry.ref });
  });

  it('從程式推測的內容標成 Inferred，停用的規則不同步', async () => {
    await seed();
    const docs = await loadNoteDocs(db, userId);
    expect(docs.find((d) => d.kind === 'concept')!.status).toBe('Inferred');
    const rules = docs.filter((d) => d.kind === 'rule');
    expect(rules.map((r) => [r.title, r.status])).toEqual([['一次只能預約一台', 'Verified']]);
    expect(parseMarker(rules[0]!.answer)).toEqual({ kind: 'rule', ref: '預約系統／實驗設備預約／一次只能預約一台' });
  });

  it('篩選專案時只帶相關的觀念，不分大小寫', async () => {
    await seed();
    await notes.saveConcept({ term: '無關觀念', plain_explanation: '沒被引用', confidence: 'general_definition' });
    const docs = await loadNoteDocs(db, userId, { projects: ['部落格'] });
    expect(docs.map((d) => d.title)).toEqual(['改首頁']);
    const booking = await loadNoteDocs(db, userId, { projects: ['預約系統'] });
    expect(booking.map((d) => d.title).sort()).toEqual(['一次只能預約一台', '黑名單', '黑名單擋預約', '預約流程怎麼串'].sort());
  });

  it('一般筆記整篇放進答案，問題是標題和專案', async () => {
    await seed();
    const doc = (await loadNoteDocs(db, userId)).find((d) => d.kind === 'article')!;
    expect(doc.question).toBe('預約流程怎麼串（預約系統）');
    expect(doc.status).toBe('Inferred');
    expect(doc.answer).toContain('## 先查黑名單\n再檢查時段衝突');
    expect(doc.answer).toContain('### 還不懂的\n取消後多久能再約？');
    expect(doc.answer).toContain('相關觀念：黑名單');
    expect(parseMarker(doc.answer)).toEqual({ kind: 'article', ref: doc.ref });
  });

  it('太長的問題會截斷', async () => {
    await notes.addEntry({ project: 'A', title: 't'.repeat(700), what_changed: 'x'.repeat(2000), why: 'y' });
    const [doc] = await loadNoteDocs(db, userId);
    expect(doc!.question.length).toBe(600);
    expect(doc!.answer).toContain('x'.repeat(2000));
  });
});

/** 用真的 MCP 伺服器（記憶體內連線），和 Claude 呼叫的是同一組工具。 */
async function mcpCaller() {
  const [clientSide, serverSide] = InMemoryTransport.createLinkedPair();
  await buildMcpServer(notes).connect(serverSide);
  const client = new Client({ name: 'test', version: '1.0.0' });
  await client.connect(clientSide);
  return notesToolCaller(client);
}

describe('loadNoteDocsViaMcp', () => {
  it('讀出的內容和直接查資料庫一樣', async () => {
    await seed();
    const viaMcp = await loadNoteDocsViaMcp(await mcpCaller());
    expect(viaMcp.complete).toBe(true);
    expect(viaMcp.docs).toEqual(await loadNoteDocs(db, userId));
  });

  it('篩選專案的結果也一樣', async () => {
    await seed();
    await notes.saveConcept({ term: '無關觀念', plain_explanation: '沒被引用', confidence: 'general_definition' });
    const viaMcp = await loadNoteDocsViaMcp(await mcpCaller(), { projects: ['預約系統'] });
    expect(viaMcp.docs).toEqual(await loadNoteDocs(db, userId, { projects: ['預約系統'] }));
  });

  it('還沒有專案時回傳空的', async () => {
    expect(await loadNoteDocsViaMcp(await mcpCaller())).toEqual({ docs: [], complete: true });
  });

  it('超過一頁（50 筆）時會翻頁讀完', async () => {
    for (let i = 0; i < 120; i++) await notes.addEntry({ project: 'A', title: `T${i}`, what_changed: 'w', why: 'y' });
    const viaMcp = await loadNoteDocsViaMcp(await mcpCaller());
    expect(viaMcp.complete).toBe(true);
    expect(viaMcp.docs).toEqual(await loadNoteDocs(db, userId));
  });

  it('舊版伺服器沒有一般筆記工具時標成不完整，其他筆記照樣讀', async () => {
    await seed();
    const call = await mcpCaller();
    const oldServer: typeof call = (name, args) => {
      if (name === 'search_articles') throw new Error('MCP error -32602: Tool search_articles not found');
      return call(name, args);
    };
    const viaMcp = await loadNoteDocsViaMcp(oldServer);
    expect(viaMcp.complete).toBe(false);
    expect(viaMcp.docs.some((d) => d.kind === 'article')).toBe(false);
    expect(viaMcp.docs.some((d) => d.kind === 'entry')).toBe(true);
  });

  it('伺服器不支援翻頁時標成不完整，呼叫端就不會刪除', async () => {
    for (let i = 0; i < 60; i++) await notes.addEntry({ project: 'A', title: `T${i}`, what_changed: 'w', why: 'y' });
    const call = await mcpCaller();
    // 模擬舊版伺服器：忽略 offset，每次都回第一頁
    const oldServer: typeof call = (name, { offset: _offset, ...args }) => call(name, args);
    const viaMcp = await loadNoteDocsViaMcp(oldServer);
    expect(viaMcp.complete).toBe(false);
    expect(viaMcp.docs).toHaveLength(50);
  });
});

const keys = (docs: NoteDoc[]) => new Set(docs.map(noteKey));

describe('buildPlan', () => {
  it('第一次全部新增，再跑一次都不變', async () => {
    await seed();
    const docs = await loadNoteDocs(db, userId);
    const { client, state } = fakeRag();
    const first = buildPlan(docs, state.questions, itemIds, { existingKeys: keys(docs) });
    expect(first.create).toHaveLength(docs.length);
    await applyPlan(client, first);

    const second = buildPlan(docs, state.questions, itemIds, { existingKeys: keys(docs) });
    expect(second).toMatchObject({ create: [], update: [], delete: [], unchanged: docs.length });
  });

  it('筆記改了就更新同一筆', async () => {
    await seed();
    const { client, state } = fakeRag();
    await applyPlan(client, buildPlan(await loadNoteDocs(db, userId), state.questions, itemIds, {}));

    await db.query(`UPDATE entries SET why = '改過的原因' WHERE title = '改首頁'`);
    const plan = buildPlan(await loadNoteDocs(db, userId), state.questions, itemIds, {});
    expect(plan.update.map((u) => u.title)).toEqual(['改首頁']);
    await applyPlan(client, plan);
    expect(state.questions.filter((q) => q.answer.includes('改過的原因'))).toHaveLength(1);
  });

  it('不碰沒有來源標記的問答；有給所有筆記的 key 才刪除已不存在的筆記', async () => {
    const manual = { id: 1, qaItemTypeID: 11, question: '手動建的', answer: '不是筆記', status: 'Verified' };
    const gone = { id: 2, qaItemTypeID: 11, question: '舊的', answer: `內容\n\n${markerFor('entry', 999)}`, status: 'Verified' };
    const docs = await loadNoteDocs(db, userId);

    expect(buildPlan(docs, [manual, gone], itemIds, {}).delete).toEqual([]);
    expect(buildPlan(docs, [manual, gone], itemIds, { existingKeys: keys(docs) }).delete).toEqual([
      { id: 2, key: 'entry#999' },
    ]);
  });

  it('只同步部分專案時，其他專案的問答保留，已不存在的筆記照樣刪除', async () => {
    await seed();
    const { client, state } = fakeRag();
    const all = await loadNoteDocs(db, userId);
    await applyPlan(client, buildPlan(all, state.questions, itemIds, { existingKeys: keys(all) }));
    const blog = all.find((d) => d.title === '改首頁')!;
    const gone = { id: 999, qaItemTypeID: 11, question: '舊的', answer: `內容

${markerFor('entry', 999)}`, status: 'Verified' };

    const booking = await loadNoteDocs(db, userId, { projects: ['預約系統'] });
    const plan = buildPlan(booking, [...state.questions, gone], itemIds, { existingKeys: keys(all) });
    expect(plan.delete).toEqual([{ id: 999, key: 'entry#999' }]);
    expect(plan.unchanged).toBe(booking.length);
    expect(plan.delete.map((d) => d.key)).not.toContain(noteKey(blog));
  });

  it('同一篇筆記重複的問答一定刪掉多的那筆', async () => {
    await notes.addEntry({ project: 'A', title: 'T', what_changed: 'w', why: 'y' });
    const [doc] = await loadNoteDocs(db, userId);
    const copy = (id: number) => ({ id, qaItemTypeID: 11, question: doc!.question, answer: doc!.answer, status: 'Verified' });
    const plan = buildPlan([doc!], [copy(1), copy(2)], itemIds, {});
    expect(plan).toMatchObject({ unchanged: 1, delete: [{ id: 2 }] });
  });
});

describe('resolveCategories', () => {
  it('預覽時不建立分類，寫入時建立且第二次沿用', async () => {
    const { client, state } = fakeRag();
    expect(await resolveCategories(client, false)).toBeNull();
    expect(state.types).toEqual([]);

    const ids = await resolveCategories(client, true);
    expect(state.items.map((i) => i.nameZh)).toEqual(['改動紀錄', '觀念', '規則', '筆記']);
    expect(await resolveCategories(client, true)).toEqual(ids);
    expect(state.types).toHaveLength(1);
  });
});
