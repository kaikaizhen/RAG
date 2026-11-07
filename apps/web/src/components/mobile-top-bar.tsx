"use client"

import { SidebarTrigger } from "@/components/ui/sidebar"

/** 手機版（含 PWA）頂部列：提供開啟側邊欄的按鈕；桌面版側邊欄常駐，不顯示 */
export function MobileTopBar({ title }: { title: string }) {
  return (
    <header className="sticky top-0 z-10 flex h-14 shrink-0 items-center gap-2 border-b bg-background px-4 md:hidden">
      <SidebarTrigger />
      <span className="font-semibold">{title}</span>
    </header>
  )
}
