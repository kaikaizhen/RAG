"use client"

import * as React from "react"
import { ChevronRight, Loader2, Plus, RefreshCw, Search, Trash2, X } from "lucide-react"
import { Button } from "@/components/ui/button"
import { MobileGroup } from "@/components/mobile/list"
import { statusMeta } from "@/lib/knowledge-status"
import type { QAPair } from "./QAList"

interface MobileAdminProps {
  qaPairs: QAPair[]
  loading: boolean
  getTypeName: (typeId: string) => string
  stats: string
  onRefresh: () => void
  onNew: () => void
  onEdit: (qa: QAPair) => void
  /** 編輯面板 */
  sheetOpen: boolean
  isEditing: boolean
  submitting: boolean
  canSubmit: boolean
  onSubmit: () => void
  onCloseSheet: () => void
  onDeleteEditing: () => void
  /** 面板內的表單欄位 */
  form: React.ReactNode
}

/**
 * 知識庫管理的手機版（含 PWA）：清單優先，新增與編輯在全螢幕面板裡進行。
 * 桌面版維持左右兩欄；這裡不重複一份表單邏輯，只負責呈現與手勢。
 */
export function MobileAdmin({ qaPairs, loading, getTypeName, stats, onRefresh, onNew, onEdit, sheetOpen, isEditing, submitting, canSubmit, onSubmit, onCloseSheet, onDeleteEditing, form }: MobileAdminProps) {
  const [query, setQuery] = React.useState("")

  const filtered = React.useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return qaPairs
    return qaPairs.filter((qa) => qa.question.toLowerCase().includes(q) || qa.answer.toLowerCase().includes(q))
  }, [qaPairs, query])

  // 面板開啟時鎖住背後的捲動
  React.useEffect(() => {
    if (!sheetOpen) return
    const previous = document.body.style.overflow
    document.body.style.overflow = "hidden"
    return () => {
      document.body.style.overflow = previous
    }
  }, [sheetOpen])

  return (
    <div className="flex min-h-full flex-col px-4 pb-24 pt-3">
      <div className="sticky top-14 z-[5] -mx-4 space-y-2 bg-muted/90 px-4 pb-3 pt-1 backdrop-blur">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="search"
            inputMode="search"
            enterKeyHint="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="搜尋問題或答案"
            aria-label="搜尋問答"
            className="h-12 w-full rounded-full border bg-background pl-10 pr-10 text-base outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
          {query && (
            <button type="button" onClick={() => setQuery("")} aria-label="清除搜尋" className="absolute right-1.5 top-1/2 flex h-9 w-9 -translate-y-1/2 items-center justify-center rounded-full text-muted-foreground active:bg-muted">
              <X className="h-4 w-4" />
            </button>
          )}
        </div>
        <div className="flex items-center justify-between px-1 text-[13px] text-muted-foreground">
          <span>{query ? `符合 ${filtered.length} / ${qaPairs.length} 筆` : stats}</span>
          <button type="button" onClick={onRefresh} className="flex h-8 items-center gap-1 rounded-full px-2 active:bg-muted" aria-label="重新載入">
            <RefreshCw className="h-3.5 w-3.5" />
            重新載入
          </button>
        </div>
      </div>

      {loading && qaPairs.length === 0 ? (
        <div className="flex justify-center py-16 text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
        </div>
      ) : filtered.length === 0 ? (
        <p className="py-16 text-center text-muted-foreground">{query ? "找不到符合的問答" : "還沒有任何問答，按右下角的 ＋ 新增"}</p>
      ) : (
        <MobileGroup>
          {filtered.map((qa) => {
            const status = statusMeta(qa.status)
            return (
              <button key={qa.id} type="button" onClick={() => onEdit(qa)} className="flex min-h-16 w-full items-center gap-3 px-4 py-3 text-left transition-colors active:bg-muted">
                <span className="min-w-0 flex-1">
                  <span className="line-clamp-2 break-words font-medium">{qa.question}</span>
                  <span className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-muted-foreground">
                    <span className="rounded-md bg-primary/10 px-1.5 py-0.5 font-medium text-primary">{getTypeName(qa.type)}</span>
                    {status && <span className={`rounded-md px-1.5 py-0.5 font-medium ${status.className}`}>{status.label}</span>}
                    <span>{new Date(qa.createdAt).toLocaleDateString("zh-TW")}</span>
                  </span>
                </span>
                <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" aria-hidden="true" />
              </button>
            )
          })}
        </MobileGroup>
      )}

      {/* 新增：右下角浮動按鈕，浮在底部分頁列上方（鍵盤開啟時分頁列隱藏，按鈕跟著降到安全區上方） */}
      {!sheetOpen && (
        <button
          type="button"
          onClick={onNew}
          aria-label="新增問答"
          className="fab-above-nav fixed right-4 z-20 flex h-14 w-14 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg transition-transform active:scale-90"
        >
          <Plus className="h-6 w-6" />
        </button>
      )}

      {sheetOpen && (
        <div role="dialog" aria-modal="true" aria-label={isEditing ? "編輯問答" : "新增問答"} className="fixed inset-x-0 top-0 z-50 flex h-dvh flex-col bg-background duration-200 animate-in slide-in-from-bottom">
          <header className="flex h-14 shrink-0 items-center justify-between border-b pl-2 pr-3">
            <button type="button" onClick={onCloseSheet} disabled={submitting} className="flex h-11 items-center rounded-full px-3 text-base active:bg-muted disabled:opacity-50">
              取消
            </button>
            <h2 className="text-lg font-bold">{isEditing ? "編輯問答" : "新增問答"}</h2>
            <Button onClick={onSubmit} disabled={!canSubmit || submitting} className="h-10 rounded-full px-5 font-semibold">
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : "儲存"}
            </Button>
          </header>
          <div className="flex-1 space-y-5 overflow-y-auto px-4 pb-[calc(2rem+env(safe-area-inset-bottom))] pt-4">
            {form}
            {isEditing && (
              <Button type="button" variant="outline" onClick={onDeleteEditing} disabled={submitting} className="h-12 w-full rounded-full text-base font-semibold text-destructive">
                <Trash2 className="mr-2 h-5 w-5" />
                刪除這則問答
              </Button>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
