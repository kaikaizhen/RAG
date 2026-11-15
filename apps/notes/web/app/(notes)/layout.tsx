import { AlertTriangle, LogOut, MessageCircle } from 'lucide-react';
import Link from 'next/link';
import type { ReactNode } from 'react';
import { Logo } from '@/components/Logo';
import { NavLinks } from '@/components/NavLinks';
import { ThemeToggle } from '@/components/ThemeToggle';
import { projectBreakdown, projectHref, projectSize, projectTone } from '@/components/ui';
import { getSharing, getStats, listProjects } from '@/lib/api';

export default async function NotesLayout({ children }: { children: ReactNode }) {
  // layout 的錯誤接不到 error.tsx，後端連不上時側欄少資訊就好，由頁面顯示錯誤
  const [stats, projects, sharing] = await Promise.all([
    getStats().catch(() => null),
    listProjects()
      .then((result) => result.projects)
      .catch(() => []),
    getSharing().catch(() => null),
  ]);
  const counts = stats && {
    entries: stats.entries,
    articles: stats.articles,
    concepts: stats.concepts,
    rules: stats.rules,
    questions: stats.open_questions,
  };
  const listed = projects.filter((project) => projectSize(project) > 0);
  // 只有設成「我的問答」的專案靠本人的登入同步；公開專案走公開帳號，不受影響
  const syncProblem =
    sharing?.projects.some((project) => project.sharing === 'private') &&
    (sharing.status.error ?? (sharing.status.linked ? null : '還沒連上問答系統，請重新登入'));

  return (
    <div className="shell">
      <aside className="sidebar">
        <Link href="/" className="brand">
          <Logo size={28} />
          學習筆記
        </Link>
        <NavLinks counts={counts} />
        {listed.length > 0 && (
          <nav className="side-projects" aria-label="依專案看筆記">
            <h2>專案</h2>
            {listed.map((project) => (
              <Link
                key={project.name}
                href={projectHref(project)}
                className={`side-project tone-${projectTone(project.name)}`}
                title={projectBreakdown(project)}
              >
                <i aria-hidden="true" />
                <span>{project.name}</span>
                <b>{projectSize(project)}</b>
              </Link>
            ))}
          </nav>
        )}
        {syncProblem && (
          <Link href="/sharing" className="side-alert" title={syncProblem} aria-label={`問答同步：${syncProblem}`}>
            <AlertTriangle size={16} strokeWidth={1.8} aria-hidden="true" />
            <span>{syncProblem}</span>
          </Link>
        )}
        <a href={process.env.RAG_WEB_URL ?? 'https://ragence.doraeyi.com'} className="to-chat" aria-label="去問答">
          <MessageCircle size={16} strokeWidth={1.8} aria-hidden="true" />
          <span>去問答</span>
        </a>
        <div className="side-actions">
          <ThemeToggle />
          <form action="/login/submit" method="post" className="logout">
            <input type="hidden" name="intent" value="logout" />
            <button type="submit">
              <LogOut size={16} strokeWidth={1.8} aria-hidden="true" />
              <span>登出</span>
            </button>
          </form>
        </div>
      </aside>
      <main className="page">{children}</main>
    </div>
  );
}
