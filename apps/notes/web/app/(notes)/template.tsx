import type { ReactNode } from 'react';

/** template 每次換頁都會重新掛載，進場動畫才會在每一頁播放。 */
export default function NotesTemplate({ children }: { children: ReactNode }) {
  return <div className="page-enter">{children}</div>;
}
