'use client';

import { BookOpen, CircleHelp, Compass, Globe, History, House, KeyRound, NotebookText, ScrollText, Share2 } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

export interface NavCounts {
  entries: number;
  articles: number;
  concepts: number;
  rules: number;
  questions: number;
}

const NAV = [
  { href: '/', label: '總覽', icon: House, count: null },
  { href: '/entries', label: '改動紀錄', icon: History, count: 'entries' },
  { href: '/articles', label: '筆記', icon: NotebookText, count: 'articles' },
  { href: '/concepts', label: '詞彙表', icon: BookOpen, count: 'concepts' },
  { href: '/rules', label: '規則', icon: ScrollText, count: 'rules' },
  { href: '/questions', label: '待確認', icon: CircleHelp, count: 'questions' },
  { href: '/explore', label: '公開知識', icon: Globe, count: null },
  { href: '/sharing', label: '分享範圍', icon: Share2, count: null },
  { href: '/tokens', label: 'Token', icon: KeyRound, count: null },
  { href: '/guide', label: '使用指南', icon: Compass, count: null },
] as const;

export function NavLinks({ counts }: { counts: NavCounts | null }) {
  const pathname = usePathname();
  return (
    <nav className="nav" aria-label="主選單">
      {NAV.map(({ href, label, icon: Icon, count }) => {
        const active = href === '/' ? pathname === '/' : pathname.startsWith(href);
        const value = count && counts ? counts[count] : null;
        return (
          <Link key={href} href={href} className="nav-link" aria-current={active ? 'page' : undefined}>
            <Icon size={16} strokeWidth={1.8} aria-hidden="true" />
            <span>{label}</span>
            {value ? <b className={`nav-count${count === 'questions' ? ' is-alert' : ''}`}>{value}</b> : null}
          </Link>
        );
      })}
    </nav>
  );
}
