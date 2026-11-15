import type { Server } from 'node:http';
import type { AddressInfo } from 'node:net';
import { serve } from '@hono/node-server';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { createApp } from '../src/app.js';
import type { Database } from '../src/db.js';
import { createNotes } from '../src/notes.js';
import { createUsers, type Users } from '../src/users.js';
import { createTestDatabase } from './helpers.js';

// 走真的 HTTP 和官方 MCP client，驗證 Claude 實際連線時的完整路徑。
const TOKEN = 'test-token-test-token-test-token-1234';
const API_TOKEN = 'read-token-read-token-read-token-1234';

let db: Database;
let users: Users;
let ownerId: number;
let server: Server;
let baseUrl: string;

beforeAll(async () => {
  db = await createTestDatabase();
  users = createUsers(db);
  ownerId = await users.originalUserId();
  const app = createApp({ db, users, mcpToken: TOKEN, apiToken: API_TOKEN });
  await new Promise<void>((resolve) => {
    server = serve({ fetch: app.fetch, port: 0, hostname: '127.0.0.1' }, () => resolve()) as Server;
  });
  baseUrl = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
});

afterAll(async () => {
  await new Promise((resolve) => server.close(resolve));
  await db.close();
});

async function connect() {
  const client = new Client({ name: 'test', version: '1.0.0' });
  await client.connect(
    new StreamableHTTPClientTransport(new URL(`${baseUrl}/mcp`), {
      requestInit: { headers: { Authorization: `Bearer ${TOKEN}` } },
    }),
  );
  return client;
}

function parse(result: Awaited<ReturnType<Client['callTool']>>) {
  const content = result.content as Array<{ text: string }>;
  return JSON.parse(content[0]!.text) as Record<string, any>;
}

describe('HTTP', () => {
  it('/health 不用 token', async () => {
    const res = await fetch(`${baseUrl}/health`);
    expect(await res.json()).toEqual({ status: 'ok' });
  });

  it.each([
    ['沒有 token', undefined],
    ['token 錯誤', 'Bearer wrong'],
    ['格式錯誤', TOKEN],
  ])('/mcp %s 回 401', async (_name, authorization) => {
    const res = await fetch(`${baseUrl}/mcp`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', ...(authorization ? { authorization } : {}) },
      body: '{}',
    });
    expect(res.status).toBe(401);
  });

  it('GET /mcp 回 405', async () => {
    const res = await fetch(`${baseUrl}/mcp`, { headers: { authorization: `Bearer ${TOKEN}` } });
    expect(res.status).toBe(405);
  });
});

describe('唯讀 API', () => {
  const get = (path: string, token: string | null = API_TOKEN) =>
    fetch(`${baseUrl}${path}`, token ? { headers: { authorization: `Bearer ${token}`, 'x-notes-user': String(ownerId) } } : {});

  beforeAll(async () => {
    const notes = createNotes(db, ownerId);
    await notes.saveConcept({ term: 'BOM', plain_explanation: '物料清單', confidence: 'general_definition', open_questions: '誰維護？' });
    await notes.addEntry({ project: 'API專案', module: '模組甲', title: '第一筆', what_changed: 'a', why: 'b' });
    await notes.addEntry({ project: 'API專案', module: '模組甲', title: '第二筆', what_changed: 'c', why: 'd' });
    await notes.updateProjectRules({
      project: 'API專案',
      module: '模組甲',
      rules: [{ title: 'API 規則', body: '內容', confidence: 'code_inferred' }],
    });
  });

  it.each(['/api/projects', '/api/stats', '/api/entries', '/api/concepts', '/api/rules'])('%s 沒帶 token 或拿 MCP token 都回 401', async (path) => {
    expect((await get(path, null)).status).toBe(401);
    expect((await get(path, TOKEN)).status).toBe(401);
  });

  it('API token 不能用在 MCP', async () => {
    const res = await fetch(`${baseUrl}/mcp`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', authorization: `Bearer ${API_TOKEN}`, 'x-notes-user': String(ownerId) },
      body: '{}',
    });
    expect(res.status).toBe(401);
  });

  it('API 只能讀，不能寫', async () => {
    const res = await fetch(`${baseUrl}/api/entries`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', authorization: `Bearer ${API_TOKEN}`, 'x-notes-user': String(ownerId) },
      body: '{}',
    });
    expect(res.status).toBe(404);
  });

  it('專案清單有模組和紀錄筆數', async () => {
    const { projects } = (await (await get('/api/projects')).json()) as { projects: unknown[] };
    expect(projects).toContainEqual(expect.objectContaining({ name: 'API專案', modules: ['模組甲'], entry_count: 2 }));
  });

  it('紀錄可以篩選和換頁', async () => {
    const first = (await (await get(`/api/entries?project=${encodeURIComponent('API專案')}&limit=1`)).json()) as any;
    const second = (await (await get(`/api/entries?project=${encodeURIComponent('API專案')}&limit=1&offset=1`)).json()) as any;
    expect(first.entries[0].title).toBe('第二筆');
    expect(second.entries[0].title).toBe('第一筆');
  });

  it('可以列出待確認的觀念和推測的規則', async () => {
    const concepts = (await (await get('/api/concepts?has_open_questions=true')).json()) as any;
    expect(concepts.concepts.map((c: { term: string }) => c.term)).toContain('BOM');
    const rules = (await (await get('/api/rules?confidence=code_inferred')).json()) as any;
    expect(rules.rules).toContainEqual(expect.objectContaining({ project: 'API專案', title: 'API 規則' }));
  });

  it('不合法的參數回 400', async () => {
    expect((await get('/api/entries?test_status=nope')).status).toBe(400);
    expect((await get('/api/concepts?limit=9999')).status).toBe(400);
  });
});

describe('MCP', () => {
  it('連線時會收到使用規則', async () => {
    const client = await connect();
    expect(client.getInstructions()).toContain('不要自己編');
    await client.close();
  });

  it('列出所有工具', async () => {
    const client = await connect();
    const { tools } = await client.listTools();
    expect(tools.map((t) => t.name).sort()).toEqual([
      'add_entry',
      'delete_article',
      'delete_concept',
      'delete_entry',
      'get_project_rules',
      'list_knowledge_gaps',
      'resolve_knowledge_gap',
      'save_article',
      'save_concept',
      'search_articles',
      'search_concepts',
      'search_entries',
      'update_entry',
      'update_project_rules',
    ]);
    await client.close();
  });

  it('九個工具走完一輪', async () => {
    const client = await connect();

    const concept = parse(await client.callTool({
      name: 'save_concept',
      arguments: { term: '料號', plain_explanation: '零件的編號', confidence: 'general_definition' },
    }));
    expect(concept.created).toBe(true);

    const added = parse(await client.callTool({
      name: 'add_entry',
      arguments: {
        project: '預約系統',
        module: '黑名單',
        title: '黑名單擋預約',
        what_changed: '送出前檢查',
        why: '違規者不能預約',
        pr_url: 'https://github.com/org/repo/pull/1',
        concept_terms: ['料號'],
      },
    }));
    expect(added.linked_concepts).toEqual(['料號']);

    const found = parse(await client.callTool({ name: 'search_entries', arguments: { query: '黑名單' } }));
    expect(found.entries[0]).toMatchObject({ id: added.id, module: '黑名單', concepts: ['料號'] });

    await client.callTool({
      name: 'update_project_rules',
      arguments: {
        project: '預約系統',
        module: '黑名單',
        rules: [{ title: '解除條件', body: '要主管核准', confidence: 'code_inferred' }],
      },
    });
    const rules = parse(await client.callTool({ name: 'get_project_rules', arguments: { project: '預約系統' } }));
    expect(rules.rules[0]).toMatchObject({ title: '解除條件', confidence: 'code_inferred' });

    const concepts = parse(await client.callTool({ name: 'search_concepts', arguments: { query: '零件' } }));
    expect(concepts.concepts[0]).toMatchObject({ term: '料號', entry_ids: [added.id] });

    const updated = parse(await client.callTool({ name: 'update_entry', arguments: { id: added.id, pr_url: '' } }));
    expect(updated.updated_fields).toEqual(['pr_url']);
    const deleted = parse(await client.callTool({ name: 'delete_entry', arguments: { id: added.id } }));
    expect(deleted.deleted.id).toBe(added.id);
    const remaining = parse(await client.callTool({ name: 'search_entries', arguments: { limit: 50 } }));
    expect(remaining.entries.map((e: { id: number }) => e.id)).not.toContain(added.id);

    const removed = parse(await client.callTool({ name: 'delete_concept', arguments: { term: '料號' } }));
    expect(removed.deleted.term).toBe('料號');

    await client.close();
  });

  it('可修正的錯誤回給 Claude，不是連線失敗', async () => {
    const client = await connect();
    const result = await client.callTool({
      name: 'save_concept',
      arguments: { term: 'x', plain_explanation: 'password=Sup3rSecret!', confidence: 'code_inferred' },
    });
    expect(result.isError).toBe(true);
    expect(JSON.stringify(result.content)).not.toContain('Sup3rSecret');
    await client.close();
  });

  it('參數不合法會被擋', async () => {
    const client = await connect();
    const result = await client.callTool({
      name: 'update_project_rules',
      arguments: { project: 'p', module: 'm', rules: [{ title: 't', body: 'b', confidence: 'guess' }] },
    });
    expect(result.isError).toBe(true);
    await client.close();
  });
});
