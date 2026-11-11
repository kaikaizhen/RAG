"use client"

import { ChevronLeft } from "lucide-react"
import { useRouter } from "next/navigation"

/**
 * 手機版（含 PWA）的子頁面頂部列：返回鍵 + 標題。桌面版有側邊欄，不顯示。
 * 子頁面是從「我的」進來的，沒有歷史可返回時（例如直接開啟網址）回到「我的」
 */
export function MobileTopBar({ title, fallbackHref = "/me" }: { title: string; fallbackHref?: string }) {
  const router = useRouter()

  const back = () => {
    if (window.history.length > 1) router.back()
    else router.push(fallbackHref)
  }

  return (
    <header className="sticky top-0 z-10 flex h-14 shrink-0 items-center gap-1 border-b bg-background px-2 md:hidden">
      <button type="button" onClick={back} aria-label="返回" className="flex h-11 w-11 items-center justify-center rounded-full transition-colors active:bg-muted">
        <ChevronLeft className="h-6 w-6" />
      </button>
      <span className="text-lg font-bold">{title}</span>
    </header>
  )
}
