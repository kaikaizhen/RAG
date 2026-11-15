import { upsertLoginUser } from '@/lib/api';
import { createSession, SESSION_COOKIE, SESSION_DAYS } from '@/lib/session';

const MAX_FAILURES = 10;
const WINDOW_MS = 5 * 60 * 1000;
/** 失敗次數依帳號分開算，有人亂試別人的帳號時不會把其他使用者一起鎖住。 */
const failures = new Map<string, number[]>();

function recentFailures(account: string, now: number): number[] {
  for (const [key, times] of failures) {
    const recent = times.filter((time) => now - time < WINDOW_MS);
    if (recent.length > 0) failures.set(key, recent);
    else failures.delete(key);
  }
  return failures.get(account) ?? [];
}

/** 用相對網址轉向，不依賴反向代理傳來的主機名稱。 */
function redirect(location: string, cookie?: string): Response {
  const headers = new Headers({ Location: location });
  if (cookie) headers.append('Set-Cookie', cookie);
  return new Response(null, { status: 303, headers });
}

function cookie(value: string, maxAge: number): string {
  const secure = process.env.NODE_ENV === 'production' ? '; Secure' : '';
  return `${SESSION_COOKIE}=${value}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${maxAge}${secure}`;
}

/** 帳號密碼交給 RAG 驗證，這裡不存密碼。refresh token 會加密保存，讓分享設定成「我的問答」的專案能背景同步。 */
async function loginToRag(account: string, password: string): Promise<{ userId: number; name: string; refreshToken?: string } | 'wrong' | 'unavailable'> {
  let res: Response;
  try {
    res = await fetch(`${process.env.RAG_API_URL}/api/v1/Auth/Login`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ account, password }),
      cache: 'no-store',
      signal: AbortSignal.timeout(15_000),
    });
  } catch {
    return 'unavailable';
  }
  if (res.status >= 500) return 'unavailable';
  if (!res.ok) return 'wrong';
  const body = (await res.json().catch(() => null)) as { userID?: unknown; name?: unknown; refreshToken?: unknown } | null;
  if (typeof body?.userID !== 'number' || body.userID < 1) return 'unavailable';
  return {
    userId: body.userID,
    name: (typeof body.name === 'string' && body.name.trim()) || account,
    refreshToken: typeof body.refreshToken === 'string' ? body.refreshToken : undefined,
  };
}

export async function POST(request: Request) {
  const form = await request.formData();

  if (form.get('intent') === 'logout') {
    return redirect('/login', cookie('', 0));
  }

  const secret = process.env.SESSION_SECRET;
  if (!secret || secret.length < 32 || !process.env.RAG_API_URL) {
    return redirect('/login?error=config');
  }

  const account = form.get('account');
  const password = form.get('password');
  if (typeof account !== 'string' || typeof password !== 'string' || !account.trim() || !password) {
    return redirect('/login?error=wrong');
  }

  const now = Date.now();
  const key = account.trim().toLowerCase();
  const recent = recentFailures(key, now);
  if (recent.length >= MAX_FAILURES) return redirect('/login?error=locked');

  const result = await loginToRag(account.trim(), password);
  if (result === 'unavailable') return redirect('/login?error=unavailable');
  if (result === 'wrong') {
    failures.set(key, [...recent, now]);
    return redirect('/login?error=wrong');
  }

  failures.delete(key);
  let user: { id: number };
  try {
    user = await upsertLoginUser(result.userId, result.name, result.refreshToken);
  } catch {
    return redirect('/login?error=unavailable');
  }
  return redirect('/', cookie(await createSession(secret, user.id, now), SESSION_DAYS * 24 * 60 * 60));
}
