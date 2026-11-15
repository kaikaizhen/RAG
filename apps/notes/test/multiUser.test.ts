import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { createApp } from '../src/app.js';
import { migrate, type Database } from '../src/db.js';
import { NotesError } from '../src/errors.js';
import { createNotes, type Notes } from '../src/notes.js';
import { loadNoteDocs } from '../src/ragSync.js';
import { createUsers, type Users } from '../src/users.js';
import { createTestDatabase, createTestUser } from './helpers.js';

let db: Database;
let users: Users;
let aliceId: number;
let bobId: number;
let alice: Notes;
let bob: Notes;

beforeEach(async () => {
  db = await createTestDatabase();
  users = createUsers(db, 42);
  aliceId = await createTestUser(db, 'Alice');
  bobId = await createTestUser(db, 'Bob');
  alice = createNotes(db, aliceId);
  bob = createNotes(db, bobId);
});
afterEach(() => db.close());

const entry = { project: '公司機密', module: '報價', title: '報價規則', what_changed: '調整折扣', why: '客戶要求' };
const rule = { title: '折扣上限', body: '最多九折', confidence: 'code_inferred' as const };
const concept = { term: 'BOM', plain_explanation: 'Alice 的解釋', confidence: 'general_definition' as const };

describe('筆記隔離', () => {
  it('看不到別人的紀錄、觀念卡、規則、專案和統計', async () => {
    const { id } = await alice.addEntry(entry);
    await alice.saveConcept(concept);
    await alice.updateProjectRules({ project: entry.project, module: entry.module, rules: [rule] });

    expect((await bob.searchEntries({})).count).toBe(0);
    expect((await bob.searchEntries({ id })).count).toBe(0);
    expect((await bob.searchEntries({ query: '報價' })).count).toBe(0);
    expect((await bob.searchConcepts({})).count).toBe(0);
    expect((await bob.listRules({})).count).toBe(0);
    expect((await bob.listProjects()).projects).toEqual([]);
    expect(await bob.getStats()).toMatchObject({ entries: 0, concepts: 0, rules: 0, code_inferred: 0 });
    expect(await alice.getStats()).toMatchObject({ entries: 1, concepts: 1, rules: 1 });
  });

  it('搜尋規則只找得到自己的', async () => {
    await alice.updateProjectRules({ project: entry.project, module: entry.module, rules: [rule] });
    expect((await alice.listRules({ query: '九折' })).count).toBe(1);
    expect((await alice.listRules({ query: '不存在的字' })).count).toBe(0);
    expect((await bob.listRules({ query: '九折' })).count).toBe(0);
  });

  it('查規則時的錯誤訊息不會洩漏別人的專案名稱', async () => {
    await alice.addEntry(entry);
    const error = await bob.getProjectRules({ project: '公司機密' }).catch((e: Error) => e);
    expect(error).toBeInstanceOf(NotesError);
    expect((error as Error).message).toContain('還沒有專案');
  });

  it('不能修改或刪除別人的紀錄，連關聯也不會被動到', async () => {
    await alice.saveConcept(concept);
    const { id } = await alice.addEntry({ ...entry, concept_terms: ['BOM'] });

    await expect(bob.updateEntry({ id, title: '被改掉' })).rejects.toThrow('找不到改動紀錄');
    await expect(bob.deleteEntry({ id })).rejects.toThrow('找不到改動紀錄');

    const { entries } = await alice.searchEntries({ id });
    expect(entries[0]).toMatchObject({ title: '報價規則', concepts: ['BOM'] });
  });

  it('不能刪除別人的觀念卡，也不能關聯別人的紀錄', async () => {
    await alice.saveConcept(concept);
    const { id } = await alice.addEntry(entry);

    await expect(bob.deleteConcept({ term: 'BOM' })).rejects.toThrow('找不到觀念卡');
    await expect(
      bob.saveConcept({ term: '偷連', plain_explanation: 'x', confidence: 'general_definition', link_entry_ids: [id] }),
    ).rejects.toThrow('找不到改動紀錄');
    expect((await alice.searchConcepts({})).count).toBe(1);
  });

  it('同名專案和觀念卡各自獨立，新增紀錄不會連到別人的觀念卡', async () => {
    await alice.saveConcept(concept);
    await alice.addEntry(entry);

    const added = await bob.addEntry({ ...entry, concept_terms: ['BOM'] });
    expect(added).toMatchObject({ project_created: true, linked_concepts: [], unresolved_concept_terms: ['BOM'] });

    await bob.saveConcept({ ...concept, plain_explanation: 'Bob 的解釋' });
    const { concepts } = await alice.searchConcepts({ query: 'BOM' });
    expect(concepts[0]).toMatchObject({ plain_explanation: 'Alice 的解釋' });
  });

  it('同步只讀得到擁有者自己的筆記', async () => {
    await alice.addEntry(entry);
    await alice.saveConcept(concept);
    await alice.updateProjectRules({ project: entry.project, module: entry.module, rules: [rule] });

    expect(await loadNoteDocs(db, bobId)).toEqual([]);
    expect(await loadNoteDocs(db, bobId, { projects: [entry.project] })).toEqual([]);
    expect(await loadNoteDocs(db, aliceId)).toHaveLength(3);
  });
});

describe('既有資料', () => {
  it('多人化之前的筆記歸給原始擁有者，登入時由設定的 RAG 帳號接手', async () => {
    const originalId = await users.originalUserId();
    await createNotes(db, originalId).addEntry(entry);

    const stranger = await users.login({ ragUserId: 7, name: '路人' });
    expect(stranger.id).not.toBe(originalId);

    const owner = await users.login({ ragUserId: 42, name: 'Liam' });
    expect(owner).toEqual({ id: originalId, name: 'Liam' });
    expect((await users.login({ ragUserId: 42, name: 'Liam 2' })).id).toBe(originalId);
    expect(await migrate(db)).toEqual([]);
  });
});

describe('MCP token', () => {
  it('明文只回傳一次，資料庫只有雜湊，撤銷後失效', async () => {
    const created = await users.createToken(aliceId, '工作電腦');
    expect(created.token).toMatch(/^ln_/);
    expect(await users.userForToken(created.token)).toBe(aliceId);

    const { rows } = await db.query<{ token_hash: string }>('SELECT token_hash FROM mcp_tokens');
    expect(rows[0]!.token_hash).not.toContain(created.token);
    expect(JSON.stringify(await users.listTokens(aliceId))).not.toContain(created.token);

    await users.revokeToken(aliceId, created.id);
    expect(await users.userForToken(created.token)).toBeNull();
    expect((await users.listTokens(aliceId)).tokens).toEqual([]);
  });

  it('不能撤銷別人的 token，也看不到', async () => {
    const created = await users.createToken(aliceId, '工作電腦');
    await expect(users.revokeToken(bobId, created.id)).rejects.toThrow('找不到 token');
    expect((await users.listTokens(bobId)).tokens).toEqual([]);
    expect(await users.userForToken(created.token)).toBe(aliceId);
  });

  it('亂打的 token 和空字串都無效', async () => {
    expect(await users.userForToken('ln_nope')).toBeNull();
    expect(await users.userForToken('')).toBeNull();
  });
});

describe('API 的使用者身分', () => {
  const API_TOKEN = 'read-token-read-token-read-token-1234';
  const MCP_TOKEN = 'test-token-test-token-test-token-1234';
  const call = (path: string, headers: Record<string, string>, init: RequestInit = {}) =>
    createApp({ db, users, mcpToken: MCP_TOKEN, apiToken: API_TOKEN }).request(path, { ...init, headers });
  const as = (userId: number | string) => ({ authorization: `Bearer ${API_TOKEN}`, 'x-notes-user': String(userId) });

  it('只回傳 X-Notes-User 那位使用者的資料', async () => {
    await alice.addEntry(entry);
    expect(((await (await call('/api/entries', as(aliceId))).json()) as any).count).toBe(1);
    expect(((await (await call('/api/entries', as(bobId))).json()) as any).count).toBe(0);
  });

  it('匯出只有自己的筆記，包含已停用的規則和已解決的問題', async () => {
    await alice.addEntry(entry);
    await alice.saveConcept({ term: '料號', plain_explanation: '零件編號', confidence: 'code_inferred' });
    await alice.updateProjectRules({ project: entry.project, module: '報價', rules: [{ title: '舊規則', body: 'x', confidence: 'code_inferred' }] });
    await alice.updateProjectRules({ project: entry.project, module: '報價', retire_titles: ['舊規則'] });
    const gap = await alice.addGap({ question: '什麼是 BOM？' });
    expect(gap.added).toBe(true);
    await alice.resolveGap({ id: (await alice.listGaps()).gaps[0]!.id, resolution: '補了觀念卡' });
    await bob.addEntry({ ...entry, title: 'Bob 的祕密' });

    const res = await call('/api/export', as(aliceId));
    expect(res.status).toBe(200);
    const data = (await res.json()) as any;
    expect(data.format).toBe('learning-notes-export');
    expect(data.entries).toHaveLength(1);
    expect(data.concepts[0]).toMatchObject({ term: '料號' });
    expect(data.rules[0]).toMatchObject({ title: '舊規則' });
    expect(data.rules[0].retired_at).not.toBeNull();
    expect(data.knowledge_gaps[0]).toMatchObject({ question: '什麼是 BOM？', resolution: '補了觀念卡' });
    expect(JSON.stringify(data)).not.toContain('Bob 的祕密');
  });

  it.each(['', 'abc', '0', '-1', '999999', '99999999999'])('X-Notes-User 是「%s」時回 401', async (value) => {
    expect((await call('/api/entries', as(value))).status).toBe(401);
  });

  it('使用者 token 拿來打 API 回 401', async () => {
    const { token } = await users.createToken(aliceId, '工作電腦');
    const res = await call('/api/entries', { authorization: `Bearer ${token}`, 'x-notes-user': String(aliceId) });
    expect(res.status).toBe(401);
  });

  it('API 建立的 token 屬於那位使用者', async () => {
    const res = await call('/api/tokens', { ...as(bobId), 'content-type': 'application/json' }, { method: 'POST', body: JSON.stringify({ name: '筆電' }) });
    expect(res.status).toBe(201);
    const { token } = (await res.json()) as { token: string };
    expect(await users.userForToken(token)).toBe(bobId);
  });

  it('沒有帶 token 不能登入', async () => {
    const res = await call('/api/users/login', { 'content-type': 'application/json' }, { method: 'POST', body: JSON.stringify({ rag_user_id: 42, name: 'x' }) });
    expect(res.status).toBe(401);
  });

  it('每位使用者的 MCP token 只通往自己的筆記', async () => {
    await alice.addEntry(entry);
    const bobToken = (await users.createToken(bobId, '筆電')).token;
    const headers = { authorization: `Bearer ${bobToken}`, 'content-type': 'application/json', accept: 'application/json, text/event-stream' };
    const body = JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'tools/call', params: { name: 'search_entries', arguments: {} } });
    const res = await call('/mcp', headers, { method: 'POST', body });
    expect(res.status).toBe(200);
    const text = await res.text();
    expect(text).toMatch(/count\\": 0/);
    expect(text).not.toContain('報價規則');
  });
});
