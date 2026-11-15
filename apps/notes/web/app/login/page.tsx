import { Logo } from '@/components/Logo';

const MESSAGES: Record<string, string> = {
  wrong: '帳號或密碼不正確。帳號和問答系統是同一組。',
  locked: '這個帳號嘗試太多次，請過幾分鐘再試。',
  unavailable: '登入服務暫時連不上，請稍後再試。',
  config: '伺服器的登入設定不完整，請檢查 RAG_API_URL 和 SESSION_SECRET。',
};

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ error?: string }> }) {
  const { error } = await searchParams;
  const message = error ? MESSAGES[error] : undefined;

  return (
    <main className="login">
      <div className="login-card">
        <Logo size={88} animated className="login-logo" />
        <h1>學習筆記</h1>
        <p className="login-hint">
          用問答系統的帳號登入。還沒有帳號？到{' '}
          <a href={`${process.env.RAG_WEB_URL ?? 'https://ragence.doraeyi.com'}/signup`}>問答系統註冊</a>
        </p>
        <form action="/login/submit" method="post">
          <label htmlFor="account">帳號</label>
          <input id="account" name="account" type="text" autoComplete="username" required autoFocus />
          <label htmlFor="password">密碼</label>
          <input id="password" name="password" type="password" autoComplete="current-password" required />
          {message && (
            <p className="login-error" role="alert">
              {message}
            </p>
          )}
          <button type="submit" className="primary-button">
            登入
          </button>
        </form>
      </div>
    </main>
  );
}
