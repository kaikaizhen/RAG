import { describe, expect, it } from 'vitest';
import { MemoryTokenStore, RagClient, RagValidationError, type ChatStreamEvent } from '../src/index.js';
import { fakeServer, session } from './fakeServer.js';

async function signedIn(fetchImpl: typeof fetch) {
  const tokenStore = new MemoryTokenStore();
  await tokenStore.set({ userId: 1, accessToken: 'access-a', refreshToken: 'refresh-a' });
  return new RagClient({ baseUrl: 'https://rag.test', tokenStore, fetch: fetchImpl });
}

function sseFetch(chunks: string[], status = 200): typeof fetch {
  const encoder = new TextEncoder();
  return async () =>
    new Response(
      new ReadableStream({
        start(controller) {
          for (const chunk of chunks) controller.enqueue(encoder.encode(chunk));
          controller.close();
        },
      }),
      { status, headers: { 'content-type': status === 200 ? 'text/event-stream' : 'application/json' } },
    );
}

async function collect(iterable: AsyncIterable<ChatStreamEvent>) {
  let text = '';
  for await (const event of iterable) if (event.type === 'text') text += event.text;
  return text;
}

describe('chat.stream', () => {
  it('逐字 SSE 串接成完整答案，空 data 代表換行，且可跨 chunk 邊界', async () => {
    const client = await signedIn(sseFetch(['data: 你\n\ndata: 好\n\nda', 'ta: \n\n\ndata: !\n\n', 'data:  \n\n']));
    expect(await collect(client.chat.stream('hi'))).toBe('你好\n! ');
  });

  it('先送 chat 事件帶出聊天室編號，再送文字', async () => {
    const client = await signedIn(sseFetch(['event: chat\ndata: 42\n\n', 'data: 好\n\n']));
    const events: ChatStreamEvent[] = [];
    for await (const event of client.chat.stream('hi')) events.push(event);
    expect(events).toEqual([
      { type: 'chat', chatId: '42' },
      { type: 'text', text: '好' },
    ]);
  });

  it('非 2xx 以 RagError 拋出', async () => {
    const client = await signedIn(sseFetch(['{"error":"找不到相關內容"}'], 404));
    await expect(collect(client.chat.stream('hi'))).rejects.toMatchObject({ status: 404 });
  });

  it('帶 Authorization 與 Space hint，並可用 signal 中止', async () => {
    let seen: Record<string, string> = {};
    const fetchImpl: typeof fetch = async (_input, init) => {
      seen = Object.fromEntries(Object.entries((init?.headers ?? {}) as Record<string, string>).map(([k, v]) => [k.toLowerCase(), v]));
      return new Response('data: a\n\n', { status: 200 });
    };
    const client = await signedIn(fetchImpl);
    client.spaces.select(7);
    await collect(client.chat.stream('hi', { signal: new AbortController().signal }));
    expect(seen.authorization).toBe('Bearer access-a');
    expect(seen['x-space-id']).toBe('7');
    expect(seen.accept).toBe('text/event-stream');
  });
});

describe('CRUD resources', () => {
  it('questions.list 依種類查詢帶 Type=1&Id；全部查詢帶 Type=0；不帶 UserID', async () => {
    const urls: string[] = [];
    const fetchImpl: typeof fetch = async (input) => {
      urls.push(String(input));
      return new Response('[]', { status: 200, headers: { 'content-type': 'application/json' } });
    };
    const client = await signedIn(fetchImpl);
    await client.questions.list();
    await client.questions.list({ qaTypeId: 3 });
    expect(urls).toEqual(['https://rag.test/api/v1/Question?Type=0', 'https://rag.test/api/v1/Question?Type=1&Id=3']);
    expect(urls.join()).not.toMatch(/UserID/i);
  });

  it('寫入端點走對的 method / path / body', async () => {
    const ok = () => ({ status: 200, body: { state: 'Success', message: 'ok' } });
    const server = fakeServer({
      'POST /api/v1/Question': ok,
      'PUT /api/v1/Question': ok,
      'DELETE /api/v1/Question/5': ok,
      'DELETE /api/v1/QaTypeItem/2': ok,
      'POST /api/v1/User': ok,
    });
    const client = await signedIn(server.fetch);
    await client.questions.create({ qaItemTypeID: 1, question: 'q', answer: 'a' });
    await client.questions.update({ id: 5, qaItemTypeID: 1, question: 'q', answer: 'a2', status: 'Stale' });
    await client.questions.delete(5);
    await client.qaTypeItems.delete(2);
    await client.users.create({ account: 'x', password: 'y' });
    expect(server.requests.map((r) => `${r.method} ${r.path}`)).toEqual([
      'POST /api/v1/Question',
      'PUT /api/v1/Question',
      'DELETE /api/v1/Question/5',
      'DELETE /api/v1/QaTypeItem/2',
      'POST /api/v1/User',
    ]);
    expect(server.requests[1]!.body).toMatchObject({ id: 5, status: 'Stale' });
    // 建立帳號需要登入
    expect(server.requests[4]!.headers.authorization).toBe('Bearer access-a');
  });

  it('register / verifyEmail 不需登入，不帶 Authorization', async () => {
    const ok = () => ({ status: 200, body: { status: 'pending', message: 'ok' } });
    const server = fakeServer({ 'POST /api/v1/Auth/Register': ok, 'POST /api/v1/Auth/VerifyEmail': ok });
    const client = new RagClient({ baseUrl: 'https://rag.test', fetch: server.fetch });

    await client.auth.register({ account: 'a@example.com', password: 'long-enough-pw', nameZh: '甲' });
    await client.auth.verifyEmail('abc');

    expect(server.requests.map((r) => `${r.method} ${r.path}`)).toEqual(['POST /api/v1/Auth/Register', 'POST /api/v1/Auth/VerifyEmail']);
    expect(server.requests.every((r) => r.headers.authorization === undefined)).toBe(true);
    expect(server.requests[1]!.body).toEqual({ token: 'abc' });
  });

  it('拒絕非正整數 id，避免組出非預期路徑', () => {
    const client = new RagClient({ baseUrl: 'https://rag.test', fetch: async () => new Response('{}') });
    expect(() => client.questions.delete(0)).toThrow(RagValidationError);
    expect(() => client.users.get(1.5)).toThrow(RagValidationError);
    expect(session(1, 'a').userID).toBe(1);
  });
});
