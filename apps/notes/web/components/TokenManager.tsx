'use client';

import { Check, Copy } from 'lucide-react';
import { useState } from 'react';
import type { McpToken } from '@/lib/types';

const dateFormat = new Intl.DateTimeFormat('zh-TW', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Taipei' });

/** 登入逾時時 proxy 會回登入頁的 HTML，所以先確認拿到的是 JSON。 */
async function readJson<T>(res: Response): Promise<T & { error?: string }> {
  if (!res.headers.get('content-type')?.includes('application/json')) {
    throw new Error('登入已過期，請重新整理頁面再登入一次。');
  }
  return (await res.json()) as T & { error?: string };
}

function CopyField({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      /* 剪貼簿被瀏覽器封鎖時，使用者還是可以自己選取文字 */
    }
  };

  return (
    <div className="token-copy">
      <span className="token-copy-label">{label}</span>
      <code>{value}</code>
      <button type="button" className="ghost-button" onClick={copy} aria-label={copied ? `已複製${label}` : `複製${label}`}>
        {copied ? <Check size={16} aria-hidden="true" /> : <Copy size={16} aria-hidden="true" />}
        {copied ? '已複製' : '複製'}
      </button>
    </div>
  );
}

export function TokenManager({ initialTokens, mcpUrl }: { initialTokens: McpToken[]; mcpUrl: string }) {
  const [tokens, setTokens] = useState(initialTokens);
  const [name, setName] = useState('');
  const [created, setCreated] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
    } catch (err) {
      setError(err instanceof Error ? err.message : '操作失敗，請稍後再試。');
    } finally {
      setBusy(false);
    }
  };

  const create = (event: React.FormEvent) => {
    event.preventDefault();
    void run(async () => {
      const res = await fetch('/tokens/manage', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ name }),
      });
      const body = await readJson<McpToken & { token: string }>(res);
      if (!res.ok) throw new Error(body.error ?? '產生失敗，請稍後再試。');
      setTokens((current) => [{ id: body.id, name: body.name, created_at: new Date().toISOString(), last_used_at: null }, ...current]);
      setCreated(body.token);
      setName('');
    });
  };

  const revoke = (token: McpToken) => {
    if (!window.confirm(`撤銷「${token.name}」？用這組 token 的 Claude 會立刻連不上。`)) return;
    void run(async () => {
      const res = await fetch(`/tokens/manage?id=${token.id}`, { method: 'DELETE' });
      const body = await readJson<unknown>(res);
      if (!res.ok) throw new Error(body.error ?? '撤銷失敗，請稍後再試。');
      setTokens((current) => current.filter((t) => t.id !== token.id));
    });
  };

  return (
    <div className="token-page">
      <form className="token-form" onSubmit={create}>
        <label htmlFor="token-name">這組 token 用在哪裡</label>
        <div className="token-form-row">
          <input
            id="token-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={50}
            placeholder="例如：家裡的電腦"
            required
          />
          <button type="submit" className="primary-button" disabled={busy}>
            產生 token
          </button>
        </div>
      </form>

      {error && (
        <p className="login-error" role="alert">
          {error}
        </p>
      )}

      {created && (
        <section className="token-created" aria-label="新的 token">
          <h2>現在就複製，離開這頁就看不到了</h2>
          <CopyField label="Token" value={created} />
          <p>在終端機執行這行，把筆記接到 Claude Code：</p>
          <CopyField
            label="設定指令"
            value={`claude mcp add --transport http learning-notes ${mcpUrl} --header "Authorization: Bearer ${created}"`}
          />
        </section>
      )}

      {tokens.length === 0 ? (
        <p className="empty">還沒有 token。產生一組，就能讓 Claude 開始記筆記。</p>
      ) : (
        <ul className="token-list">
          {tokens.map((token) => (
            <li key={token.id}>
              <div>
                <strong>{token.name}</strong>
                <span>
                  {dateFormat.format(new Date(token.created_at))} 建立
                  {' · '}
                  {token.last_used_at ? `最後使用 ${dateFormat.format(new Date(token.last_used_at))}` : '還沒用過'}
                </span>
              </div>
              <button type="button" className="ghost-button" disabled={busy} onClick={() => revoke(token)}>
                撤銷
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
