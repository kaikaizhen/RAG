import { setSharing, type Sharing } from '@/lib/api';

const VALID: Sharing[] = ['off', 'private', 'public'];

export async function PUT(request: Request) {
  const body = (await request.json().catch(() => null)) as { project?: unknown; sharing?: unknown } | null;
  if (typeof body?.project !== 'string' || !VALID.includes(body.sharing as Sharing)) {
    return Response.json({ error: 'invalid body' }, { status: 400 });
  }
  const result = await setSharing(body.project, body.sharing as Sharing);
  return Response.json(result.body, { status: result.status });
}
