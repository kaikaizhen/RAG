"use client"

import * as React from "react"
import Link from "next/link"
import { usePathname } from "next/navigation"
import { History, Inbox, MessageSquare, User } from "lucide-react"
import { cn } from "@/lib/utils"
import { getClient } from "@/lib/rag"

interface NavItem {
  href: string
  label: string
  icon: React.ComponentType<{ className?: string; strokeWidth?: number }>
  /** 這些路徑底下的頁面也算在這個分頁 */
  match: (pathname: string) => boolean
}

const ITEMS: NavItem[] = [
  { href: "/", label: "問答", icon: MessageSquare, match: (p) => p === "/" },
  { href: "/history", label: "歷史", icon: History, match: (p) => p.startsWith("/history") },
  { href: "/inbox", label: "暫存區", icon: Inbox, match: (p) => p.startsWith("/inbox") },
  {
    href: "/me",
    label: "我的",
    icon: User,
    match: (p) => ["/me", "/profile", "/developer", "/admin-settings"].some((base) => p === base || p.startsWith(base + "/")),
  },
]

/**
 * 手機版（含 PWA）底部分頁列：只在窄螢幕顯示，取代桌面版的側邊欄。
 * 點擊區至少 64px 高；底部避開 iOS 的 Home Indicator；輸入文字時（鍵盤開啟）暫時隱藏，把空間讓給輸入。
 */
export function BottomNav() {
  const pathname = usePathname()
  const [inboxCount, setInboxCount] = React.useState(0)

  React.useEffect(() => {
    const rag = getClient()
    const load = () => rag.inbox.list().then((list) => setInboxCount(list.length)).catch(() => setInboxCount(0))
    void load()
    window.addEventListener("inboxUpdated", load)
    const offSpace = rag.on("space-changed", () => void load())
    return () => {
      window.removeEventListener("inboxUpdated", load)
      offSpace()
    }
  }, [])

  // 鍵盤：輸入框取得焦點 = 鍵盤開啟。用 html 的屬性讓 CSS 一併調整版面（見 globals.css 的 .mobile-main）
  React.useEffect(() => {
    const root = document.documentElement
    const isTextField = (el: EventTarget | null) =>
      el instanceof HTMLElement && (el.tagName === "TEXTAREA" || (el.tagName === "INPUT" && !["checkbox", "radio", "button", "submit"].includes((el as HTMLInputElement).type)))
    const onIn = (e: FocusEvent) => {
      if (isTextField(e.target)) root.dataset.kb = "open"
    }
    const onOut = () => {
      // 在兩個輸入框之間移動焦點時 focusout 後馬上會 focusin，延後一下避免閃動
      setTimeout(() => {
        if (!isTextField(document.activeElement)) delete root.dataset.kb
      }, 50)
    }
    document.addEventListener("focusin", onIn)
    document.addEventListener("focusout", onOut)
    return () => {
      document.removeEventListener("focusin", onIn)
      document.removeEventListener("focusout", onOut)
      delete root.dataset.kb
    }
  }, [])

  return (
    <nav
      aria-label="主要導航"
      className="mobile-nav fixed inset-x-0 bottom-0 z-30 grid grid-cols-4 border-t bg-background pb-[env(safe-area-inset-bottom)] md:hidden"
    >
      {ITEMS.map((item) => {
        const active = item.match(pathname)
        const Icon = item.icon
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "flex min-h-16 flex-col items-center justify-center gap-1 py-2 text-muted-foreground transition-colors",
              active && "text-foreground",
            )}
          >
            <span
              className={cn(
                "relative flex h-8 w-14 items-center justify-center rounded-full transition-[background-color,transform] duration-200 active:scale-90",
                active && "bg-muted",
              )}
            >
              <Icon className="h-6 w-6" strokeWidth={active ? 2.4 : 2} />
              {item.href === "/inbox" && inboxCount > 0 && (
                <span
                  className="absolute right-1.5 top-0 min-w-4 rounded-full bg-primary px-1 text-center text-[10px] font-semibold leading-4 text-primary-foreground"
                  aria-label={`${inboxCount} 筆待審`}
                >
                  {inboxCount > 99 ? "99+" : inboxCount}
                </span>
              )}
            </span>
            <span className={cn("text-xs", active ? "font-semibold" : "font-medium")}>{item.label}</span>
          </Link>
        )
      })}
    </nav>
  )
}
