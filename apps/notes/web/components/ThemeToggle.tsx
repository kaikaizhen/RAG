'use client';

import { Moon, Sun } from 'lucide-react';

/** 伺服器不知道目前的主題，所以兩種文字都輸出，由 CSS 依主題只顯示一種。 */
export function ThemeToggle() {
  const toggle = () => {
    const root = document.documentElement;
    const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
    root.dataset.theme = next;
    try {
      localStorage.setItem('theme', next);
    } catch {
      /* 瀏覽器不讓存時，這次還是切得過去，只是下次不會記得 */
    }
  };

  return (
    <button type="button" className="theme-toggle" onClick={toggle}>
      <span className="theme-to-dark">
        <Moon size={16} strokeWidth={1.8} aria-hidden="true" />
        <span className="theme-label">深色模式</span>
      </span>
      <span className="theme-to-light">
        <Sun size={16} strokeWidth={1.8} aria-hidden="true" />
        <span className="theme-label">淺色模式</span>
      </span>
    </button>
  );
}
