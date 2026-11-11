import * as React from "react"
import Link from "next/link"
import { Check, ChevronRight } from "lucide-react"
import { cn } from "@/lib/utils"

/** 手機版的頁面標題（大字，和底部分頁列搭配）。桌面版不用 */
export function MobileTitle({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <header className="mb-5 flex items-center justify-between gap-3 md:hidden">
      <h1 className="text-[28px] font-bold leading-tight tracking-tight">{title}</h1>
      {children}
    </header>
  )
}

/** 一組列：白底圓角卡片，列與列之間用細線分隔（設定頁的樣式） */
export function MobileGroup({ label, children, className }: { label?: string; children: React.ReactNode; className?: string }) {
  return (
    <section className={cn("space-y-2", className)}>
      {label && <h2 className="ml-1 text-[13px] font-semibold text-muted-foreground">{label}</h2>}
      <div className="overflow-hidden rounded-2xl bg-background shadow-sm [&>*+*]:border-t">{children}</div>
    </section>
  )
}

interface RowProps {
  icon?: React.ReactNode
  label: string
  value?: string
  /** 有 href 就是連結；有 onClick 就是按鈕；都沒有就只是顯示 */
  href?: string
  onClick?: () => void
  /** 目前選中的項目（顯示打勾） */
  selected?: boolean
  /** 右側自訂內容（例如開關）；有的話不顯示箭頭 */
  trailing?: React.ReactNode
  danger?: boolean
  disabled?: boolean
}

/** 一列：至少 56px 高的點擊區，圖示 + 文字 + 右側箭頭 / 打勾 / 自訂內容 */
export function MobileRow({ icon, label, value, href, onClick, selected, trailing, danger, disabled }: RowProps) {
  const body = (
    <>
      {icon && <span className="flex h-6 w-6 shrink-0 items-center justify-center">{icon}</span>}
      <span className="min-w-0 flex-1 truncate font-medium">{label}</span>
      {value && <span className="max-w-[45%] truncate text-muted-foreground">{value}</span>}
      {trailing ?? (selected ? <Check className="h-5 w-5 shrink-0" aria-hidden="true" /> : href ? <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" aria-hidden="true" /> : null)}
    </>
  )
  const className = cn(
    "flex min-h-14 w-full items-center gap-3 px-4 py-3 text-left transition-colors active:bg-muted",
    danger && "text-destructive",
    disabled && "pointer-events-none opacity-50",
  )

  if (href) {
    return (
      <Link href={href} className={className}>
        {body}
      </Link>
    )
  }
  if (onClick) {
    return (
      <button type="button" onClick={onClick} className={className} aria-pressed={selected}>
        {body}
      </button>
    )
  }
  return <div className={className}>{body}</div>
}
