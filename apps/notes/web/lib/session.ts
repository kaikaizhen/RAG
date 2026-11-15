/** 登入狀態存在簽章過的 cookie：`到期時間.使用者.簽章`。沒有密鑰就改不了到期時間，也換不了使用者。 */

export const SESSION_COOKIE = 'notes_session';
export const SESSION_DAYS = 14;

function key(secret: string) {
  return crypto.subtle.importKey('raw', new TextEncoder().encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, [
    'sign',
    'verify',
  ]);
}

export async function createSession(secret: string, userId: number, now = Date.now()): Promise<string> {
  const payload = `${now + SESSION_DAYS * 24 * 60 * 60 * 1000}.${userId}`;
  const signature = await crypto.subtle.sign('HMAC', await key(secret), new TextEncoder().encode(payload));
  return `${payload}.${Buffer.from(signature).toString('base64url')}`;
}

/** 有效時回傳使用者 id，否則回傳 null。 */
export async function verifySession(value: string | undefined, secret: string | undefined, now = Date.now()): Promise<number | null> {
  if (!value || !secret) return null;
  const [expires, userId, signature, ...rest] = value.split('.');
  if (!expires || !userId || !signature || rest.length > 0 || !/^\d+$/.test(expires) || !/^\d+$/.test(userId)) return null;
  if (Number(expires) <= now) return null;
  const ok = await crypto.subtle.verify(
    'HMAC',
    await key(secret),
    Buffer.from(signature, 'base64url'),
    new TextEncoder().encode(`${expires}.${userId}`),
  );
  return ok ? Number(userId) : null;
}
