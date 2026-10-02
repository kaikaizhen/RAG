import { describe, expect, it, vi } from 'vitest';
import {
  MemoryTokenStore,
  RagApiError,
  RagClient,
  RagConflictError,
  RagForbiddenError,
  RagNotFoundError,
  RagServiceUnavailableError,
  RagUnauthorizedError,
  RagValidationError,
} from '../src/index.js';
import { fakeServer, session } from './fakeServer.js';

const BASE = 'http://api.test';

function signedIn(routes: Parameters<typeof fakeServer>[0]) {
  const server = fakeServer({ 'POST /api/v1/Auth/Login': () => ({ status: 200, body: session(1, 'a') }), ...routes });
  const store = new MemoryTokenStore();
  const client = new RagClient({ baseUrl: `${BASE}/`, fetch: server.fetch, tokenStore: store });
  return { server, store, client, login: () => client.auth.login('userA', 'passA') };
}

describe('session', () => {
  it('login is sent anonymously, stores the session and emits session-changed', async () => {
    const { server, store, client } = signedIn({});
    const events: unknown[] = [];
    client.on('session-changed', (e) => events.push(e));

    const result = await client.auth.login('userA', 'passA');

    expect(result.userID).toBe(1);
    expect(server.requests[0]).toMatchObject({ method: 'POST', path: '/api/v1/Auth/Login', body: { account: 'userA', password: 'passA' } });
    expect(server.requests[0]!.headers.authorization).toBeUndefined();
    expect(await store.get()).toEqual({ userId: 1, accessToken: 'access-a', refreshToken: 'refresh-a' });
    expect(events).toEqual([{ userId: 1 }]);
  });

  it('failed login does not try to refresh', async () => {
    const server = fakeServer({ 'POST /api/v1/Auth/Login': () => ({ status: 401, body: { error: '登入失敗' } }) });
    const client = new RagClient({ baseUrl: BASE, fetch: server.fetch });

    await expect(client.auth.login('x', 'y')).rejects.toBeInstanceOf(RagUnauthorizedError);
    expect(server.requests.map((r) => r.path)).toEqual(['/api/v1/Auth/Login']);
  });

  it('requests without a session fail locally without calling the API', async () => {
    const server = fakeServer({});
    const client = new RagClient({ baseUrl: BASE, fetch: server.fetch });

    await expect(client.spaces.list()).rejects.toBeInstanceOf(RagUnauthorizedError);
    expect(server.requests).toHaveLength(0);
  });

  it('logout clears session, cache and space selection', async () => {
    const { client, store, server, login } = signedIn({ 'GET /api/v1/Space': () => ({ status: 200, body: [] }) });
    await login();
    client.spaces.select(2);
    await client.spaces.list();
    const events: unknown[] = [];
    client.on('session-changed', (e) => events.push(e));

    await client.auth.logout();

    expect(await store.get()).toBeNull();
    expect(client.spaces.selected()).toBeNull();
    expect(events).toEqual([null]);
    await expect(client.spaces.list()).rejects.toBeInstanceOf(RagUnauthorizedError);
    expect(server.requests.filter((r) => r.path === '/api/v1/Space')).toHaveLength(1);
  });
});

describe('token attachment and space hint', () => {
  it('attaches bearer token and the selected space hint only', async () => {
    const { client, server, login } = signedIn({ 'GET /api/v1/Chat': () => ({ status: 200, body: [] }) });
    await login();

    await client.chat.history();
    client.spaces.select(7);
    await client.chat.history();

    const [first, second] = server.requests.filter((r) => r.path === '/api/v1/Chat');
    expect(first!.headers.authorization).toBe('Bearer access-a');
    expect(first!.headers['x-space-id']).toBeUndefined();
    expect(second!.headers['x-space-id']).toBe('7');
  });

  it('space selection is validated for format only and does not claim authorization', async () => {
    const { client, login } = signedIn({ 'GET /api/v1/Space/current': () => ({ status: 403, body: { error: 'Forbidden space' } }) });
    await login();

    expect(() => client.spaces.select(0)).toThrow(RagValidationError);
    expect(() => client.spaces.select(1.5)).toThrow(RagValidationError);
    client.spaces.select(99);
    await expect(client.spaces.current()).rejects.toBeInstanceOf(RagForbiddenError);
  });

  it('space change clears session-bound cached metadata and emits space-changed', async () => {
    let calls = 0;
    const { client, server, login } = signedIn({
      'GET /api/v1/Skill/effective': (r) => ({ status: 200, body: { spaceKey: `space-${r.headers['x-space-id'] ?? 'auto'}-${++calls}`, skills: [] } }),
    });
    await login();
    const events: unknown[] = [];
    client.on('space-changed', (e) => events.push(e));

    const a1 = await client.skills.effective();
    const a2 = await client.skills.effective();
    client.spaces.select(2);
    const b = await client.skills.effective();

    expect(a1).toBe(a2);
    expect(b.spaceKey).toBe('space-2-2');
    expect(events).toEqual([{ spaceId: 2 }]);
    expect(server.requests.filter((r) => r.path === '/api/v1/Skill/effective')).toHaveLength(2);
  });

  it('switching to a different user resets the space selection', async () => {
    let n = 0;
    const server = fakeServer({ 'POST /api/v1/Auth/Login': () => ({ status: 200, body: session(++n, String(n)) }) });
    const client = new RagClient({ baseUrl: BASE, fetch: server.fetch });

    await client.auth.login('a', 'a');
    client.spaces.select(5);
    await client.auth.login('b', 'b');

    expect(client.spaces.selected()).toBeNull();
  });
});

describe('401 handling', () => {
  it('refreshes once for concurrent 401s and retries with the new token', async () => {
    const { client, server, store, login } = signedIn({
      'GET /api/v1/Chat': (r) => (r.headers.authorization === 'Bearer access-b' ? { status: 200, body: [] } : { status: 401, body: { error: 'Unauthorized' } }),
      'POST /api/v1/Auth/Refresh': async (r) => {
        expect(r.body).toEqual({ refreshToken: 'refresh-a' });
        expect(r.headers.authorization).toBeUndefined();
        await new Promise((resolve) => setTimeout(resolve, 10));
        return { status: 200, body: session(1, 'b') };
      },
    });
    await login();

    await Promise.all([client.chat.history(), client.chat.history(), client.chat.history()]);

    expect(server.requests.filter((r) => r.path === '/api/v1/Auth/Refresh')).toHaveLength(1);
    expect((await store.get())!.accessToken).toBe('access-b');
  });

  it('failed refresh clears the session, emits session-expired and throws 401', async () => {
    const { client, store, login } = signedIn({
      'GET /api/v1/Space': () => ({ status: 200, body: [] }),
      'GET /api/v1/Chat': () => ({ status: 401, body: { error: 'Unauthorized' } }),
      'POST /api/v1/Auth/Refresh': () => ({ status: 401, body: { error: 'Unauthorized' } }),
    });
    await login();
    client.spaces.select(3);
    await client.spaces.list();
    const expired = vi.fn();
    client.on('session-expired', expired);

    await expect(client.chat.history()).rejects.toBeInstanceOf(RagUnauthorizedError);

    expect(expired).toHaveBeenCalledOnce();
    expect(await store.get()).toBeNull();
    expect(client.spaces.selected()).toBeNull();
  });

  it('does not loop when the retried request is still 401', async () => {
    const { client, server, login } = signedIn({
      'GET /api/v1/Chat': () => ({ status: 401 }),
      'POST /api/v1/Auth/Refresh': () => ({ status: 200, body: session(1, 'b') }),
    });
    await login();

    await expect(client.chat.history()).rejects.toBeInstanceOf(RagUnauthorizedError);
    expect(server.requests.filter((r) => r.path === '/api/v1/Chat')).toHaveLength(2);
    expect(server.requests.filter((r) => r.path === '/api/v1/Auth/Refresh')).toHaveLength(1);
  });
});

describe('context (skills bundle / knowledge search)', () => {
  it('knowledge search sends the query with the space hint and is never cached', async () => {
    const { client, server, login } = signedIn({
      'POST /api/v1/Knowledge/search': (r) => ({ status: 200, body: { spaceID: 2, status: 'insufficient', evidence: [], similar: [], notice: `q=${(r.body as { query: string }).query}` } }),
    });
    await login();
    client.spaces.select(2);

    const first = await client.knowledge.search('請假流程');
    await client.knowledge.search('請假流程');

    expect(first.status).toBe('insufficient');
    const calls = server.requests.filter((r) => r.path === '/api/v1/Knowledge/search');
    expect(calls).toHaveLength(2);
    expect(calls[0]).toMatchObject({ method: 'POST', body: { query: '請假流程', type: null } });
    expect(calls[0]!.headers['x-space-id']).toBe('2');
  });

  it('skill bundle is cached per space and budget, cleared on space change, and validates budget', async () => {
    const { client, server, login } = signedIn({ 'GET /api/v1/Skill/bundle': () => ({ status: 200, body: { spaceKey: 'default', skills: [], tokenEstimate: 0, tokenBudget: 8000 } }) });
    await login();

    await client.skills.bundle();
    await client.skills.bundle();
    await client.skills.bundle(4000);
    client.spaces.select(3);
    await client.skills.bundle();
    await expect(client.skills.bundle(0)).rejects.toBeInstanceOf(RagValidationError);

    const calls = server.requests.filter((r) => r.path === '/api/v1/Skill/bundle');
    expect(calls).toHaveLength(3);
  });
});

describe('error mapping', () => {
  it.each([
    [400, RagValidationError],
    [403, RagForbiddenError],
    [404, RagNotFoundError],
    [409, RagConflictError],
    [503, RagServiceUnavailableError],
    [500, RagApiError],
  ])('maps %i to a typed error with the server message', async (status, type) => {
    const { client, login } = signedIn({ 'DELETE /api/v1/Chat/9': () => ({ status, body: { error: `server says ${status}` } }) });
    await login();

    const error = await client.chat.delete('9').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(type);
    expect(error).toMatchObject({ status, message: `server says ${status}` });
  });

  it('uses ProblemDetails.detail (not the generic title) as the error message', async () => {
    const problem = { type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5', title: 'NotFound', status: 404, detail: '找不到足夠可靠的相關資料', instance: 'POST /api/v1/Chat' };
    const { client, login } = signedIn({ 'POST /api/v1/Chat': () => ({ status: 404, body: problem }) });
    await login();

    const error = await client.chat.send('hi').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(RagNotFoundError);
    expect(error).toMatchObject({ status: 404, message: '找不到足夠可靠的相關資料', body: problem });
  });

  it('url-encodes path parameters', async () => {
    const { client, server, login } = signedIn({});
    await login();

    await client.chat.logs('../Space').catch(() => undefined);

    expect(server.requests.at(-1)!.path).toBe('/api/v1/Chat/..%2FSpace');
  });
});
