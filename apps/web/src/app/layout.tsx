import type { Metadata, Viewport } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import { PwaRegister } from "@/components/pwa-register";
import { SITE_DESCRIPTION, SITE_NAME, SITE_TAGLINE, siteUrl } from "@/lib/site";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  // 需要絕對網址的 canonical / og:image 以 NEXT_PUBLIC_SITE_URL 為準（正式環境務必設定）
  metadataBase: siteUrl() ?? new URL("http://localhost:3000"),
  title: { default: `${SITE_NAME}：${SITE_TAGLINE}`, template: `%s | ${SITE_NAME}` },
  description: SITE_DESCRIPTION,
  applicationName: SITE_NAME,
  // 預設不收錄：App 內頁都需要登入、屬於私人內容；只有公開的 /about 明確開放（見 about/page.tsx）
  robots: { index: false, follow: false },
  openGraph: { type: "website", locale: "zh_TW", siteName: SITE_NAME, title: SITE_NAME, description: SITE_DESCRIPTION },
  twitter: { card: "summary_large_image", title: SITE_NAME, description: SITE_DESCRIPTION },
  // 圖示由 src/app/icon.png、apple-icon.png 與 manifest.ts 提供
  appleWebApp: {
    capable: true,
    title: "RAGence",
    statusBarStyle: "default",
  },
};

export const viewport: Viewport = {
  // 讓版面延伸到瀏海與底部安全區；各處以 env(safe-area-inset-*) 自行留白（底部分頁列已處理）
  viewportFit: "cover",
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#ffffff" },
    { media: "(prefers-color-scheme: dark)", color: "#0a0a0a" },
  ],
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="zh-TW">
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased`}
      >
        {children}
        <PwaRegister />
      </body>
    </html>
  );
}