import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { createApp } from '../src/app.js';
import type { Database } from '../src/db.js';
import { createNotes, listPublicNotes, type Notes } from '../src/notes.js';
import { createRagWorker } from '../src/ragWorker.js';
import type { RagClient, RemoteQuestion } from '../src/ragSync.js';
import { createUsers, type Users } from '../src/users.js';
import { createTestDatabase, createTestUser } from './helpers.js';

const KEY = 'k'.repeat(40);
let db: Database;
let users: Users;
let aliceId: number;
let bobId: number;
let alice: Notes;
let bob: Notes;

beforeEach(async () => {
  db = await createTestDatabase();
  users = createUsers(db, undefined, KEY);
  aliceId = await createTestUser(db, 'Alice');
  bobId = await createTestUser(db, 'Bob');
  alice = createNotes(db, aliceId);
  bob = createNotes(db, bobId);
});
afterEach(() => db.close());

const entry = (project: string) => ({ project, title: `${project} 的紀錄`, what_changed: '改了', why: '因為' });

/** 依帳號分開的假 Space，用來確認內容寫到誰那裡。 */
function fakeSpaces() {
  const spaces = new Map<string, RemoteQuestion[]>();
  let nextId = 1;
  const client = (key: string, refreshToken?: string): RagClient & { refreshToken?: string } => {
    const questions = spaces.get(key) ?? [];
    spaces.set(key, questions);
    return {
      refreshToken,
      listTypes: async () => [{ id: 1, nameZh: '學習筆記' }],
      createType: async () => undefined,
      listItems: async () => [
        { id: 11, qaTypeID: 1, nameZh: '改動紀錄' },
        { id: 12, qaTypeID: 1, nameZh: '觀念' },
        { id: 13, qaTypeID: 1, nameZh: '規則' },
        { id: 14, qaTypeID: 1, nameZh: '筆記' },
      ],
      createItem: async () => undefined,
      listQuestions: async () => [...questions],
      createQuestion: async (p: any) => void questions.push({ ...p, id: nextId++ }),
      updateQuestion: async (p: any) => void Object.assign(questions.find((q) => q.id === p.id)!, p),
      deleteQuestion: async (id: number) => void questions.splice(questions.findIndex((q) => q.id === id), 1),
    } as unknown as RagClient;
  };
  return { spaces, client };
}

function workerWith(fake: ReturnType<typeof fakeSpaces>, publicSpace = true) {
  return createRagWorker({
    db,
    users,
    clientFor: async (auth) => ('refreshToken' in auth ? fake.client(auth.refreshToken, `${auth.refreshToken}`) : fake.client('public')),
    publicSpace: publicSpace ? { account: 'svc', password: 'x', spaceId: 99 } : undefined,
  });
}

describe('分享範圍', () => {
  it('新專案預設不離開筆記', async () => {
    await alice.addEntry(entry('公司'));
    expect((await alice.listSharing()).projects[0]).toMatchObject({ name: '公司', sharing: 'off' });
  });

  it('不能設定別人的專案，也不能搶已公開的名稱', async () => {
    await alice.addEntry(entry('教學'));
    await expect(bob.setSharing({ project: '教學', sharing: 'public' })).rejects.toThrow('找不到專案');
    await alice.setSharing({ project: '教學', sharing: 'public' });
    await bob.addEntry(entry('教學'));
    await expect(bob.setSharing({ project: '教學', sharing: 'public' })).rejects.toThrow('同名');
  });

  it('公開頁只看得到公開專案', async () => {
    await alice.addEntry(entry('教學'));
    await alice.addEntry(entry('公司'));
    await bob.addEntry(entry('私人'));
    await alice.setSharing({ project: '教學', sharing: 'public' });
    await bob.setSharing({ project: '私人', sharing: 'private' });

    const list = await listPublicNotes(db, {});
    expect(list.projects.map((p) => p.name)).toEqual(['教學']);
    expect((await listPublicNotes(db, { project: '公司' })).entries).toEqual([]);
    expect((await listPublicNotes(db, { project: '私人' })).entries).toEqual([]);
    expect((await listPublicNotes(db, { project: '教學' })).entries).toHaveLength(1);
  });
});

describe('背景同步', () => {
  async function link() {
    await users.saveRefreshToken(aliceId, 'alice-space');
    await users.saveRefreshToken(bobId, 'bob-space');
  }

  it('refresh token 在資料庫是加密的，金鑰不對就讀不到', async () => {
    await link();
    const { rows } = await db.query<{ rag_refresh_enc: string }>('SELECT rag_refresh_enc FROM users WHERE id = $1', [aliceId]);
    expect(rows[0]!.rag_refresh_enc).not.toContain('alice-space');
    expect(await users.refreshTokenFor(aliceId)).toBe('alice-space');
    expect(await createUsers(db, undefined, 'z'.repeat(40)).refreshTokenFor(aliceId)).toBeNull();
  });

  it('off 不送、private 只進本人的 Space、public 只進公開 Space', async () => {
    await link();
    await alice.addEntry(entry('公司'));
    await alice.addEntry(entry('自己'));
    await alice.addEntry(entry('教學'));
    await alice.setSharing({ project: '自己', sharing: 'private' });
    await alice.setSharing({ project: '教學', sharing: 'public' });

    const fake = fakeSpaces();
    const worker = workerWith(fake);
    worker.markDirty(aliceId);
    worker.markDirty(bobId);
    await worker.tick();

    const titles = (key: string) => (fake.spaces.get(key) ?? []).map((q) => q.answer).join('\n');
    expect(titles('alice-space')).toContain('自己 的紀錄');
    expect(titles('alice-space')).not.toContain('公司 的紀錄');
    expect(titles('alice-space')).not.toContain('教學 的紀錄');
    expect(titles('bob-space')).toBe('');
    expect(titles('public')).toContain('教學 的紀錄');
    expect(titles('public')).not.toContain('公司 的紀錄');
    expect(titles('public')).not.toContain('自己 的紀錄');
    expect((await users.syncStatus(aliceId)).error).toBeNull();
  });

  it('改回不分享後，RAG 上的內容會被移除', async () => {
    await link();
    await alice.addEntry(entry('自己'));
    await alice.addEntry(entry('教學'));
    await alice.setSharing({ project: '自己', sharing: 'private' });
    await alice.setSharing({ project: '教學', sharing: 'public' });

    const fake = fakeSpaces();
    const worker = workerWith(fake);
    worker.markDirty(aliceId);
    await worker.tick();
    expect(fake.spaces.get('alice-space')).toHaveLength(1);
    expect(fake.spaces.get('public')).toHaveLength(1);

    await alice.setSharing({ project: '自己', sharing: 'off' });
    await alice.setSharing({ project: '教學', sharing: 'off' });
    worker.markDirty(aliceId);
    await worker.tick();
    expect(fake.spaces.get('alice-space')).toEqual([]);
    expect(fake.spaces.get('public')).toEqual([]);
  });

  it('沒有連結的使用者不會被同步', async () => {
    await alice.addEntry(entry('自己'));
    await alice.setSharing({ project: '自己', sharing: 'private' });
    const fake = fakeSpaces();
    const worker = workerWith(fake, false);
    worker.markDirty(aliceId);
    await worker.tick();
    expect(fake.spaces.size).toBe(0);
  });

  it('暫時性的失敗下一輪會重試', async () => {
    await link();
    let attempts = 0;
    const worker = createRagWorker({
      db,
      users,
      clientFor: async () => {
        attempts += 1;
        throw new Error('RAG POST /Question 失敗（500）：x');
      },
    });
    worker.markDirty(aliceId);
    await worker.tick();
    await worker.tick();
    expect(attempts).toBe(2);
    expect(await users.refreshTokenFor(aliceId)).toBe('alice-space');
  });

  it('透過 MCP 讀筆記不觸發同步，寫入才會', async () => {
    const marked: number[] = [];
    const worker = { markDirty: (id: number) => void marked.push(id), tick: async () => undefined };
    const { token } = await users.createToken(aliceId, '筆電');
    const app = createApp({ db, users, worker, mcpToken: 'x'.repeat(40), apiToken: 'y'.repeat(40) });
    const call = (name: string, args: object) =>
      app.request('/mcp', {
        method: 'POST',
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json', accept: 'application/json, text/event-stream' },
        body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'tools/call', params: { name, arguments: args } }),
      });

    await (await call('search_entries', {})).text();
    expect(marked).toEqual([]);
    await (await call('add_entry', entry('自己'))).text();
    expect(marked).toEqual([aliceId]);
  });

  it('refresh token 過期就清掉並提示重新登入', async () => {
    await link();
    const worker = createRagWorker({
      db,
      users,
      clientFor: async () => {
        throw new Error('RAG POST /Auth/Refresh 失敗（401）：x');
      },
    });
    await worker.syncUser(aliceId);
    expect(await users.refreshTokenFor(aliceId)).toBeNull();
    expect((await users.syncStatus(aliceId)).error).toContain('重新登入');
  });
});

describe('公開時的保護', () => {
  it('有疑似個資或機密的專案不能公開', async () => {
    await alice.updateProjectRules({ project: '教學', module: '聯絡', rules: [{ title: '窗口', body: '請聯絡 amy@example.com', confidence: 'code_inferred' }] }).catch(() => undefined);
    await db.query(`INSERT INTO projects (owner_id, name) VALUES ($1, '舊專案')`, [aliceId]);
    await db.query(
      `INSERT INTO entries (project_id, title, what_changed, why)
       SELECT id, '舊紀錄', '請聯絡 amy@example.com', 'x' FROM projects WHERE name = '舊專案'`,
    );
    await expect(alice.setSharing({ project: '舊專案', sharing: 'public' })).rejects.toThrow('不適合公開');
    expect((await alice.listSharing()).projects.find((p: any) => p.name === '舊專案')).toMatchObject({ sharing: 'off' });
    await expect(alice.setSharing({ project: '舊專案', sharing: 'private' })).resolves.toMatchObject({ sharing: 'private' });
  });

  it('觀念卡跟著所屬專案同步，沒歸屬的不同步', async () => {
    await users.saveRefreshToken(aliceId, 'alice-space');
    await alice.addEntry(entry('公司'));
    await alice.saveConcept({ term: '九階料號', plain_explanation: '料號的一種', confidence: 'code_inferred', project: '公司' });
    await alice.saveConcept({ term: '沒歸屬', plain_explanation: '還沒分類', confidence: 'code_inferred' });
    await expect(
      alice.saveConcept({ term: '打錯字', plain_explanation: 'x', confidence: 'code_inferred', project: '公 司' }),
    ).rejects.toThrow('找不到專案');

    const fake = fakeSpaces();
    const worker = workerWith(fake);
    const answers = () => (fake.spaces.get('alice-space') ?? []).map((q) => q.answer).join('\n');

    await worker.syncUser(aliceId);
    expect(answers()).not.toContain('九階料號');

    await alice.setSharing({ project: '公司', sharing: 'private' });
    await worker.syncUser(aliceId);
    expect(answers()).toContain('九階料號');
    expect(answers()).not.toContain('沒歸屬');

    const sharing = await alice.listSharing();
    expect(sharing.projects[0]).toMatchObject({ name: '公司', concept_count: 1 });
    expect(sharing.unassigned_concepts).toBe(1);

    // 更新內容但沒傳 project 時保留原本的專案；傳空字串才取消
    await alice.saveConcept({ term: '九階料號', plain_explanation: '改寫', confidence: 'code_inferred' });
    expect((await alice.searchConcepts({ query: '九階料號' })).concepts[0]).toMatchObject({ project: '公司' });
    await alice.saveConcept({ term: '九階料號', plain_explanation: '改寫', confidence: 'code_inferred', project: '' });
    await worker.syncUser(aliceId);
    expect(answers()).not.toContain('九階料號');
  });

  it('公開前也會檢查歸到這個專案的觀念卡', async () => {
    await alice.addEntry(entry('教學'));
    // 機密檢查加上之前留下的舊觀念卡，直接寫進資料庫模擬
    await db.query(
      `INSERT INTO concepts (term, plain_explanation, confidence, owner_id, project_id)
       SELECT '聯絡窗口', '找 amy@example.com', 'code_inferred', $1, id FROM projects WHERE name = '教學'`,
      [aliceId],
    );
    await expect(alice.setSharing({ project: '教學', sharing: 'public' })).rejects.toThrow('觀念「聯絡窗口」');
  });

  it('公開的內容不帶同事名字和沒有公開的觀念', async () => {
    await users.saveRefreshToken(aliceId, 'alice-space');
    await alice.saveConcept({ term: '公開觀念', plain_explanation: 'a', confidence: 'colleague_confirmed', confirmed_by: '王小明' });
    await alice.saveConcept({ term: '機密代號', plain_explanation: 'b', confidence: 'general_definition', related_terms: ['公開觀念'] });
    await alice.addEntry({ ...entry('教學'), concept_terms: ['公開觀念'] });
    await alice.updateProjectRules({ project: '教學', module: 'm', rules: [{ title: 'r', body: 'x', confidence: 'colleague_confirmed', confirmed_by: '王小明' }] });
    await alice.setSharing({ project: '教學', sharing: 'public' });

    const fake = fakeSpaces();
    await workerWith(fake).syncPublic();
    const text = fake.spaces.get('public')!.map((q) => q.answer).join('\n');
    expect(text).toContain('公開觀念');
    expect(text).not.toContain('王小明');
    expect(text).not.toContain('機密代號');

    const { rules } = await listPublicNotes(db, { project: '教學' });
    expect(JSON.stringify(rules)).not.toContain('王小明');
  });

  it('使用者名稱是 email 時不會顯示在公開頁', async () => {
    await db.query(`UPDATE users SET name = 'amy@example.com' WHERE id = $1`, [aliceId]);
    await alice.addEntry(entry('教學'));
    await alice.setSharing({ project: '教學', sharing: 'public' });
    expect((await listPublicNotes(db, {})).projects[0]!.owner).toBe('匿名使用者');
  });
});

describe('知識缺口', () => {
  it('各看各的，同一題不重複，關閉後可以再問', async () => {
    expect(await alice.addGap({ question: '什麼是 BOM？' })).toMatchObject({ added: true });
    expect(await alice.addGap({ question: '什麼是 bom？' })).toMatchObject({ added: false });
    expect((await bob.listGaps()).count).toBe(0);

    const { gaps } = (await alice.listGaps()) as { gaps: Array<{ id: number }> };
    await expect(bob.resolveGap({ id: gaps[0]!.id })).rejects.toThrow('找不到');
    await alice.resolveGap({ id: gaps[0]!.id, resolution: '觀念卡 BOM' });
    expect((await alice.listGaps()).count).toBe(0);
    expect(await alice.addGap({ question: '什麼是 BOM？' })).toMatchObject({ added: true });
  });

  it('問題裡有疑似機密會被拒絕', async () => {
    await expect(alice.addGap({ question: 'password=hunter2hunter2 這是什麼' })).rejects.toThrow();
  });
});
