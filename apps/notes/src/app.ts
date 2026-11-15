import { createHash, timingSafeEqual } from 'node:crypto';
import { WebStandardStreamableHTTPServerTransport } from '@modelcontextprotocol/sdk/server/webStandardStreamableHttp.js';
import { Hono } from 'hono';
import { bodyLimit } from 'hono/body-limit';
import { z } from 'zod';
import type { Database } from './db.js';
import { NotesError } from './errors.js';
import { buildMcpServer } from './mcp.js';
import { CONFIDENCE, createNotes, listPublicNotes, SHARING, TEST_STATUS, type Notes } from './notes.js';
import type { RagWorker } from './ragWorker.js';
import type { Users } from './users.js';

const paging = {
  id: z.coerce.number().int().positive().optional(),
  limit: z.coerce.number().int().min(1).max(50).optional(),
  offset: z.coerce.number().int().min(0).optional(),
};
const entriesQuery = z.object({
  query: z.string().optional(),
  project: z.string().optional(),
  module: z.string().optional(),
  test_status: z.enum(TEST_STATUS).optional(),
  ...paging,
});
const conceptsQuery = z.object({
  query: z.string().optional(),
  confidence: z.enum(CONFIDENCE).optional(),
  has_open_questions: z.enum(['true', 'false']).transform((v) => v === 'true').optional(),
  project: z.string().optional(),
  ...paging,
});
const articlesQuery = z.object({
  query: z.string().optional(),
  project: z.string().optional(),
  module: z.string().optional(),
  confidence: z.enum(CONFIDENCE).optional(),
  has_open_questions: z.enum(['true', 'false']).transform((v) => v === 'true').optional(),
  ...paging,
});
const rulesQuery = z.object({
  query: z.string().optional(),
  project: z.string().optional(),
  module: z.string().optional(),
  confidence: z.enum(CONFIDENCE).optional(),
});

const loginBody = z.object({
  rag_user_id: z.number().int().positive(),
  name: z.string().trim().min(1).max(100),
  rag_refresh_token: z.string().min(1).max(8000).optional(),
});
const sharingBody = z.object({ project: z.string().trim().min(1), sharing: z.enum(SHARING) });
const gapBody = z.object({ question: z.string().trim().min(1).max(500) });
const resolveBody = z.object({ resolution: z.string().max(500).optional() });
const tokenBody = z.object({ name: z.string().trim().min(1).max(50) });

function bearer(header: string | undefined): string {
  return header?.startsWith('Bearer ') ? header.slice('Bearer '.length) : '';
}

/** 先雜湊再比對，長度不同也不會提早結束，避免從回應時間猜 token。 */
function tokenMatches(header: string | undefined, token: string): boolean {
  const provided = bearer(header);
  const hash = (value: string) => createHash('sha256').update(value).digest();
  return timingSafeEqual(hash(provided), hash(token));
}

/** 只有真的寫入才需要同步，讀筆記不該觸發對 RAG 的呼叫。 */
function trackWrites(notes: Notes, onWrite: () => void): Notes {
  const after =
    <A, R>(run: (input: A) => Promise<R>) =>
    async (input: A) => {
      const result = await run(input);
      onWrite();
      return result;
    };
  return {
    ...notes,
    addEntry: after(notes.addEntry),
    updateEntry: after(notes.updateEntry),
    deleteEntry: after(notes.deleteEntry),
    updateProjectRules: after(notes.updateProjectRules),
    saveConcept: after(notes.saveConcept),
    deleteConcept: after(notes.deleteConcept),
    saveArticle: after(notes.saveArticle),
    deleteArticle: after(notes.deleteArticle),
  };
}

export function createApp(deps: { db: Database; users: Users; worker?: Pick<RagWorker, 'markDirty' | 'tick'>; mcpToken: string; apiToken: string }) {
  const app = new Hono<{ Variables: { userId: number } }>();
  const notesOf = (userId: number) => createNotes(deps.db, userId);

  app.get('/health', async (c) => {
    try {
      await deps.db.query('SELECT 1');
      return c.json({ status: 'ok' });
    } catch {
      return c.json({ status: 'db_unavailable' }, 503);
    }
  });

  app.use('/mcp', async (c, next) => {
    // MCP_TOKEN 是多人化之前就在用的 token，只代表原始擁有者；其他人用自己在網站產生的 token
    const header = c.req.header('authorization');
    const userId = tokenMatches(header, deps.mcpToken)
      ? await deps.users.originalUserId()
      : await deps.users.userForToken(bearer(header));
    if (userId === null) {
      return c.json({ error: 'unauthorized' }, 401, { 'WWW-Authenticate': 'Bearer' });
    }
    c.set('userId', userId);
    await next();
  });

  app.use('/mcp', bodyLimit({ maxSize: 1024 * 1024, onError: (c) => c.json({ error: 'payload too large' }, 413) }));

  // 無狀態模式：每個請求各建一組 server 和 transport，不需要 session，重啟也不會斷。
  app.post('/mcp', async (c) => {
    const userId = c.get('userId');
    const server = buildMcpServer(trackWrites(notesOf(userId), () => deps.worker?.markDirty(userId)));
    const transport = new WebStandardStreamableHTTPServerTransport({
      sessionIdGenerator: undefined,
      enableJsonResponse: true,
    });
    await server.connect(transport);
    try {
      return await transport.handleRequest(c.req.raw);
    } finally {
      await server.close();
    }
  });

  // 無狀態模式沒有伺服器推播的串流，也沒有 session 可以結束。
  app.on(['GET', 'DELETE'], '/mcp', (c) => c.json({ error: 'method not allowed' }, 405, { Allow: 'POST' }));

  // 給前端伺服器用的 API，不對外開放（Nginx 只轉 /mcp 和 /health）。筆記只有 GET，前端就算出事也改不了筆記；
  // 前端驗過登入 cookie 後用 X-Notes-User 告訴這裡是哪位使用者。
  app.use('/api/*', async (c, next) => {
    if (!tokenMatches(c.req.header('authorization'), deps.apiToken)) {
      return c.json({ error: 'unauthorized' }, 401, { 'WWW-Authenticate': 'Bearer' });
    }
    await next();
  });

  app.post('/api/users/login', async (c) => {
    const input = loginBody.safeParse(await c.req.json().catch(() => null));
    if (!input.success) return c.json({ error: 'invalid body' }, 400);
    const user = await deps.users.login({ ragUserId: input.data.rag_user_id, name: input.data.name });
    if (input.data.rag_refresh_token) await deps.users.saveRefreshToken(user.id, input.data.rag_refresh_token);
    return c.json(user);
  });

  app.use('/api/*', async (c, next) => {
    if (c.req.path === '/api/users/login') return next();
    const raw = c.req.header('x-notes-user') ?? '';
    const userId = /^[1-9]\d{0,9}$/.test(raw) ? Number(raw) : 0;
    if (userId === 0 || userId > 2147483647 || !(await deps.users.exists(userId))) {
      return c.json({ error: 'unknown user' }, 401);
    }
    c.set('userId', userId);
    await next();
  });

  app.get('/api/tokens', async (c) => c.json(await deps.users.listTokens(c.get('userId'))));

  app.post('/api/tokens', async (c) => {
    const input = tokenBody.safeParse(await c.req.json().catch(() => null));
    if (!input.success) return c.json({ error: '請輸入 1 到 50 字的名稱' }, 400);
    return c.json(await deps.users.createToken(c.get('userId'), input.data.name), 201);
  });

  app.delete('/api/tokens/:id', async (c) => {
    const id = z.coerce.number().int().positive().max(2147483647).safeParse(c.req.param('id'));
    if (!id.success) return c.json({ error: 'invalid id' }, 400);
    return c.json(await deps.users.revokeToken(c.get('userId'), id.data));
  });

  app.get('/api/sharing', async (c) => {
    const userId = c.get('userId');
    return c.json({ ...(await notesOf(userId).listSharing()), status: await deps.users.syncStatus(userId) });
  });

  app.put('/api/sharing', async (c) => {
    const input = sharingBody.safeParse(await c.req.json().catch(() => null));
    if (!input.success) return c.json({ error: 'invalid body' }, 400);
    const result = await notesOf(c.get('userId')).setSharing(input.data);
    deps.worker?.markDirty(c.get('userId'));
    void deps.worker?.tick().catch((err) => console.error('背景同步失敗:', err));
    return c.json(result);
  });

  app.get('/api/public', async (c) => c.json(await listPublicNotes(deps.db, { project: c.req.query('project') })));

  app.get('/api/gaps', async (c) => c.json(await notesOf(c.get('userId')).listGaps()));

  app.post('/api/gaps', async (c) => {
    const input = gapBody.safeParse(await c.req.json().catch(() => null));
    if (!input.success) return c.json({ error: '問題要在 1 到 500 字之間' }, 400);
    return c.json(await notesOf(c.get('userId')).addGap(input.data), 201);
  });

  app.post('/api/gaps/:id/resolve', async (c) => {
    const id = z.coerce.number().int().positive().max(2147483647).safeParse(c.req.param('id'));
    const input = resolveBody.safeParse(await c.req.json().catch(() => ({})));
    if (!id.success || !input.success) return c.json({ error: 'invalid request' }, 400);
    return c.json(await notesOf(c.get('userId')).resolveGap({ id: id.data, ...input.data }));
  });

  app.get('/api/projects', async (c) => c.json(await notesOf(c.get('userId')).listProjects()));

  app.get('/api/export', async (c) => c.json(await notesOf(c.get('userId')).exportAll()));

  app.get('/api/stats', async (c) => c.json(await notesOf(c.get('userId')).getStats()));

  app.get('/api/entries', async (c) => {
    const input = entriesQuery.safeParse(c.req.query());
    if (!input.success) return c.json({ error: 'invalid query' }, 400);
    return c.json(await notesOf(c.get('userId')).searchEntries(input.data));
  });

  app.get('/api/concepts', async (c) => {
    const input = conceptsQuery.safeParse(c.req.query());
    if (!input.success) return c.json({ error: 'invalid query' }, 400);
    return c.json(await notesOf(c.get('userId')).searchConcepts(input.data));
  });

  app.get('/api/articles', async (c) => {
    const input = articlesQuery.safeParse(c.req.query());
    if (!input.success) return c.json({ error: 'invalid query' }, 400);
    return c.json(await notesOf(c.get('userId')).searchArticles(input.data));
  });

  app.get('/api/rules', async (c) => {
    const input = rulesQuery.safeParse(c.req.query());
    if (!input.success) return c.json({ error: 'invalid query' }, 400);
    return c.json(await notesOf(c.get('userId')).listRules(input.data));
  });

  app.onError((err, c) => {
    if (err instanceof NotesError) return c.json({ error: err.message }, 400);
    console.error('API 錯誤:', err.message);
    return c.json({ error: 'internal error' }, 500);
  });

  return app;
}
