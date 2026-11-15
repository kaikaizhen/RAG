import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import './globals.css';

export const metadata: Metadata = {
  title: '學習筆記',
  description: '改動紀錄、規則和觀念卡',
  robots: { index: false, follow: false },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
};

// 要在畫面畫出來之前決定主題，否則深色使用者每次載入都會先閃一下白色。
// 沒選過就跟系統設定，系統切換時也跟著變；多個分頁之間同步。
const THEME_SCRIPT = `(function(){var d=document.documentElement,m=matchMedia('(prefers-color-scheme: dark)');function a(){var t=null;try{t=localStorage.getItem('theme')}catch(e){}d.dataset.theme=t==='light'||t==='dark'?t:m.matches?'dark':'light'}a();m.addEventListener('change',a);addEventListener('storage',a)})()`;

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="zh-Hant" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: THEME_SCRIPT }} />
      </head>
      <body>{children}</body>
    </html>
  );
}
