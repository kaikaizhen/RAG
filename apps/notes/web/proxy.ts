import { NextResponse, type NextRequest } from 'next/server';
import { SESSION_COOKIE, verifySession } from '@/lib/session';

/**
 * 所有頁面都要先登入。站在 Cloudflare 後面也要自己檢查，
 * 因為有人可以略過 Cloudflare 直接連主機。
 */
export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  if (pathname === '/login' || pathname === '/login/submit') return NextResponse.next();

  const userId = await verifySession(request.cookies.get(SESSION_COOKIE)?.value, process.env.SESSION_SECRET);
  if (userId !== null) return NextResponse.next();

  // 直接顯示登入頁，不轉址，所以不依賴反向代理傳來的主機名稱
  return NextResponse.rewrite(new URL('/login', request.url));
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
};
