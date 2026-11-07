export interface RecordedRequest {
  method: string;
  path: string;
  headers: Record<string, string>;
  body: unknown;
}

type Handler = (request: RecordedRequest) => { status: number; body?: unknown } | Promise<{ status: number; body?: unknown }>;

/** 以路由表模擬 API，記錄每個請求的 method / path / headers / body */
export function fakeServer(routes: Record<string, Handler>) {
  const requests: RecordedRequest[] = [];

  const fetchImpl: typeof fetch = async (input, init) => {
    const url = new URL(typeof input === 'string' ? input : input instanceof URL ? input.href : input.url);
    const headers = Object.fromEntries(Object.entries((init?.headers ?? {}) as Record<string, string>).map(([k, v]) => [k.toLowerCase(), v]));
    const request: RecordedRequest = {
      method: init?.method ?? 'GET',
      path: url.pathname,
      headers,
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    };
    requests.push(request);

    const handler = routes[`${request.method} ${request.path}`];
    const result = handler ? await handler(request) : { status: 404, body: { error: 'no route' } };
    return new Response(result.body === undefined ? null : JSON.stringify(result.body), {
      status: result.status,
      headers: { 'content-type': 'application/json' },
    });
  };

  return { fetch: fetchImpl, requests };
}

export const session = (userId: number, suffix: string) => ({
  userID: userId,
  name: `user-${userId}`,
  accessToken: `access-${suffix}`,
  refreshToken: `refresh-${suffix}`,
  manage: 'free',
});
