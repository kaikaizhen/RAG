import { describe, expect, it } from 'vitest';
import { MemoryTokenStore, RagClient, RagForbiddenError, RagNotFoundError, RagUnauthorizedError } from '../src/index.js';

/**
 * 對真實 .NET API 的 contract 測試（未設定環境變數時略過）：
 *   RAG_E2E_BASE_URL、RAG_E2E_ACCOUNT、RAG_E2E_PASSWORD、RAG_E2E_FORBIDDEN_SPACE（該帳號不是成員的 Space ID）
 */
const env = {
  baseUrl: process.env.RAG_E2E_BASE_URL,
  account: process.env.RAG_E2E_ACCOUNT,
  password: process.env.RAG_E2E_PASSWORD,
  forbiddenSpace: Number(process.env.RAG_E2E_FORBIDDEN_SPACE),
};

describe.skipIf(!env.baseUrl)('e2e against real API', () => {
  const newClient = (store = new MemoryTokenStore()) => ({ store, client: new RagClient({ baseUrl: env.baseUrl!, tokenStore: store }) });

  it('login → spaces → current → skills → chat history', async () => {
    const { client } = newClient();

    const login = await client.auth.login(env.account!, env.password!);
    expect(login.userID).toBeGreaterThan(0);

    const spaces = await client.spaces.list();
    expect(spaces.length).toBeGreaterThan(0);

    const current = await client.spaces.current();
    expect(spaces.map((s) => s.spaceID)).toContain(current.spaceID);

    const skills = await client.skills.effective();
    expect(skills.skills?.every((s) => typeof s.id === 'string' && typeof s.version === 'string' && typeof s.source === 'string')).toBe(true);

    expect(Array.isArray(await client.chat.history())).toBe(true);
  });

  it('unauthorized space selection is 403 and does not fall back to another space', async () => {
    const { client } = newClient();
    await client.auth.login(env.account!, env.password!);

    client.spaces.select(env.forbiddenSpace);
    await expect(client.spaces.current()).rejects.toBeInstanceOf(RagForbiddenError);
    await expect(client.chat.history()).rejects.toBeInstanceOf(RagForbiddenError);
  });

  it('foreign or missing resources are 404', async () => {
    const { client } = newClient();
    await client.auth.login(env.account!, env.password!);

    await expect(client.chat.logs('999999')).rejects.toBeInstanceOf(RagNotFoundError);
  });

  it('expired access token is transparently refreshed; invalid refresh token ends the session', async () => {
    const { client, store } = newClient();
    await client.auth.login(env.account!, env.password!);
    const session = (await store.get())!;

    await store.set({ ...session, accessToken: 'invalid.access.token' });
    expect(Array.isArray(await client.chat.history())).toBe(true);
    expect((await store.get())!.accessToken).not.toBe('invalid.access.token');

    await store.set({ ...(await store.get())!, accessToken: 'invalid', refreshToken: 'invalid' });
    await expect(client.chat.history()).rejects.toBeInstanceOf(RagUnauthorizedError);
    expect(await store.get()).toBeNull();
  });

  it('refresh token cannot be used as an access token', async () => {
    const { client, store } = newClient();
    await client.auth.login(env.account!, env.password!);
    const session = (await store.get())!;

    await store.set({ ...session, accessToken: session.refreshToken, refreshToken: 'invalid' });
    await expect(client.spaces.list()).rejects.toBeInstanceOf(RagUnauthorizedError);
  });
});
