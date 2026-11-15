'use client';

import { Download, Globe, Lock, MessageCircle } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';
import type { Sharing, SharingProject, SyncStatus } from '@/lib/api';

const OPTIONS: Array<{ value: Sharing; label: string; hint: string; icon: typeof Lock }> = [
  { value: 'off', label: '只留在筆記', hint: '不離開筆記，問答系統問不到', icon: Lock },
  { value: 'private', label: '我的問答', hint: '只有你登入問答時問得到', icon: MessageCircle },
  { value: 'public', label: '公開', hint: '所有登入的人都問得到、看得到', icon: Globe },
];

const dateFormat = new Intl.DateTimeFormat('zh-TW', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Taipei' });

export function SharingManager({
  projects,
  unassignedConcepts,
  status,
}: {
  projects: SharingProject[];
  unassignedConcepts: number;
  status: SyncStatus;
}) {
  const [rows, setRows] = useState(projects);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const change = async (project: string, sharing: Sharing) => {
    if (sharing === 'public' && !window.confirm(`把「${project}」公開？所有登入的人都能看到、問到裡面的內容，請先確認沒有公司或私人資料。`)) return;
    setBusy(project);
    setError(null);
    try {
      const res = await fetch('/sharing/manage', {
        method: 'PUT',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ project, sharing }),
      });
      const isJson = res.headers.get('content-type')?.includes('application/json');
      const body = isJson ? ((await res.json()) as { error?: string }) : null;
      if (!isJson) throw new Error('登入已過期，請重新整理頁面再登入一次。');
      if (!res.ok) throw new Error(body?.error ?? '設定失敗，請稍後再試。');
      setRows((current) => current.map((row) => (row.name === project ? { ...row, sharing } : row)));
    } catch (err) {
      setError(err instanceof Error ? err.message : '設定失敗，請稍後再試。');
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="sharing">
      <div className={`sync-status${status.error || !status.linked ? ' is-warning' : ''}`} role="status">
        <span className="sync-dot" aria-hidden="true" />
        {status.error ??
          (!status.linked
            ? '還沒連上問答系統。請登出再登入一次，「我的問答」才會開始同步。'
            : status.synced_at
              ? `同步正常，最後一次是 ${dateFormat.format(new Date(status.synced_at))}`
              : '已連上問答系統，等待第一次同步。')}
      </div>

      {error && (
        <p className="login-error" role="alert">
          {error}
        </p>
      )}

      {rows.length === 0 ? (
        <p className="empty">還沒有專案。讓 Claude 記下第一筆，專案就會出現在這裡。</p>
      ) : (
        <ul className="sharing-list">
          {rows.map((row) => (
            <li key={row.name}>
              <div className="sharing-name">
                <strong>{row.name}</strong>
                <span>
                  {row.entry_count} 筆紀錄 · {row.article_count} 篇筆記 · {row.rule_count} 條規則 · {row.concept_count} 張觀念卡
                </span>
              </div>
              <div className="segmented" role="radiogroup" aria-label={`${row.name} 的分享範圍`}>
                {OPTIONS.map(({ value, label, hint, icon: Icon }) => (
                  <button
                    key={value}
                    type="button"
                    role="radio"
                    aria-checked={row.sharing === value}
                    title={hint}
                    disabled={busy === row.name}
                    className={`tone-${value}`}
                    onClick={() => row.sharing !== value && void change(row.name, value)}
                  >
                    <Icon size={14} aria-hidden="true" />
                    {label}
                  </button>
                ))}
              </div>
            </li>
          ))}
        </ul>
      )}

      {unassignedConcepts > 0 && (
        <p className="sharing-unassigned" role="note">
          有 <Link href="/concepts">{unassignedConcepts} 張觀念卡</Link>
          沒有所屬專案，也沒連到任何紀錄，問答系統問不到。請 Claude 幫它們設定所屬專案，例如「把料號、BOM 這幾張觀念卡歸到 global-system」。
        </p>
      )}

      <section className="export-box">
        <div>
          <strong>帶走你的筆記</strong>
          <p>下載自己的所有紀錄、觀念卡、規則（含已停用的）和待補問題，存成一個 JSON 檔。只包含你自己的資料。</p>
        </div>
        <a href="/export" className="ghost-button" download>
          <Download size={14} aria-hidden="true" />
          下載全部筆記
        </a>
      </section>

      <p className="sharing-note">
        改成「只留在筆記」後，內容會在下次同步時從問答系統移除，通常一分鐘內完成。觀念卡跟著所屬專案，或它連到的紀錄所在的專案一起分享。
      </p>
    </div>
  );
}
