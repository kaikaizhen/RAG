import { createMcpToken, revokeMcpToken } from '@/lib/api';

/** 使用者在設定頁建立或撤銷 token。身分由 lib/api 從登入 cookie 取得，不看瀏覽器傳來的 id。 */
export async function POST(request: Request) {
  const body = (await request.json().catch(() => null)) as { name?: unknown } | null;
  const result = await createMcpToken(typeof body?.name === 'string' ? body.name : '');
  return Response.json(result.body, { status: result.status, headers: { 'Cache-Control': 'no-store' } });
}

export async function DELETE(request: Request) {
  const id = new URL(request.url).searchParams.get('id') ?? '';
  if (!/^\d+$/.test(id)) return Response.json({ error: 'invalid id' }, { status: 400 });
  const result = await revokeMcpToken(id);
  return Response.json(result.body, { status: result.status });
}
