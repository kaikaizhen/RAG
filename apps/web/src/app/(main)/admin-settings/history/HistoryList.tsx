"use client"

import * as React from "react"
import { Card, CardContent } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Checkbox } from "@/components/ui/checkbox"
import { Pencil, Trash2, Check, X, Loader2, ChevronRight } from "lucide-react"
import { ScrollArea } from "@/components/ui/scroll-area"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"

import type { KnowledgeStatus } from "@rag/client-sdk"
import { statusMeta } from "@/lib/knowledge-status"

export interface HistoryItem {
  id: string
  question: string
  answer: string
  qaItemTypeID?: number
  status?: KnowledgeStatus
  createdAt: string
  updatedAt?: string
}

export interface HistoryListProps {
  items: HistoryItem[]
  loading: boolean
  onDelete: (id: string) => Promise<void>
  onEdit: (item: HistoryItem, newQuestion: string, newAnswer: string) => Promise<void>
  showSelection: boolean
  selectedIds: Set<string>
  onToggleSelect: (id: string) => void
}

export function HistoryList({ 
  items, 
  loading, 
  onDelete, 
  onEdit,
  showSelection,
  selectedIds,
  onToggleSelect,
}: HistoryListProps) {
  const [expandedId, setExpandedId] = React.useState<string | null>(null)
  const [editingId, setEditingId] = React.useState<string | null>(null)
  const [editQuestion, setEditQuestion] = React.useState("")
  const [editAnswer, setEditAnswer] = React.useState("")
  const [deleteId, setDeleteId] = React.useState<string | null>(null)
  const [isDeleting, setIsDeleting] = React.useState(false)
  const [isSaving, setIsSaving] = React.useState(false)

  const handleItemClick = (itemId: string) => {
    if (showSelection) {
      onToggleSelect(itemId)
      return
    }
    if (editingId === itemId) return
    setExpandedId(expandedId === itemId ? null : itemId)
  }

  const handleEditStart = (e: React.MouseEvent, item: HistoryItem) => {
    e.stopPropagation()
    setEditingId(item.id)
    setEditQuestion(item.question)
    setEditAnswer(item.answer)
    setExpandedId(item.id)
  }

  const handleEditCancel = () => {
    setEditingId(null)
    setEditQuestion("")
    setEditAnswer("")
  }

  const handleEditSave = async (item: HistoryItem) => {
    if (!editQuestion.trim() || !editAnswer.trim()) {
      alert("問題和答案不能為空")
      return
    }

    setIsSaving(true)
    try {
      await onEdit(item, editQuestion, editAnswer)
      setEditingId(null)
      setEditQuestion("")
      setEditAnswer("")
    } catch (error) {
      console.error("編輯失敗:", error)
    } finally {
      setIsSaving(false)
    }
  }

  const handleDeleteClick = (e: React.MouseEvent, itemId: string) => {
    e.stopPropagation()
    setDeleteId(itemId)
  }

  const handleDeleteConfirm = async () => {
    if (!deleteId) return

    setIsDeleting(true)
    try {
      await onDelete(deleteId)
      setDeleteId(null)
      if (expandedId === deleteId) {
        setExpandedId(null)
      }
    } catch (error) {
      console.error("刪除失敗:", error)
    } finally {
      setIsDeleting(false)
    }
  }

  const formatDate = (dateString: string) => {
    const date = new Date(dateString)
    const now = new Date()
    const diffInMs = now.getTime() - date.getTime()
    const diffInMinutes = Math.floor(diffInMs / (1000 * 60))
    const diffInHours = Math.floor(diffInMs / (1000 * 60 * 60))
    const diffInDays = Math.floor(diffInMs / (1000 * 60 * 60 * 24))
    
    if (diffInMinutes < 60) {
      return diffInMinutes < 1 ? "剛剛" : `${diffInMinutes} 分鐘前`
    } else if (diffInHours < 24) {
      return `${diffInHours} 小時前`
    } else if (diffInDays < 7) {
      return `${diffInDays} 天前`
    } else {
      return date.toLocaleDateString("zh-TW", {
        month: "numeric",
        day: "numeric",
        year: date.getFullYear() !== now.getFullYear() ? "numeric" : undefined,
      })
    }
  }

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="flex items-center gap-2 text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          <span>載入中...</span>
        </div>
      </div>
    )
  }

  if (items.length === 0) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center text-muted-foreground">
          <p className="text-lg">暫無歷史紀錄</p>
          <p className="text-sm mt-2">開始新增問答後，會在這裡顯示</p>
        </div>
      </div>
    )
  }

  return (
    <>
      <div className="flex-1 overflow-hidden">
        <ScrollArea className="h-full">
          <div className="space-y-2 pb-4">
            {items.map((item) => {
              const isExpanded = expandedId === item.id
              const isEditing = editingId === item.id
              const isSelected = selectedIds.has(item.id)

              return (
                <Card 
                  key={item.id} 
                  className={`relative transition-all m-2 ${
                    showSelection ? 'cursor-pointer' : 'cursor-pointer hover:shadow-md'
                  } ${
                    isExpanded && !showSelection ? 'ring-2 ring-primary' : ''
                  } ${
                    isSelected ? 'bg-accent' : ''
                  }`}
                  onClick={() => handleItemClick(item.id)}
                >
                  <CardContent className="pt-3">
                    {/* 問題標題列 */}
                    <div className="flex items-start gap-3">
                        {showSelection && (
                        <Checkbox
                          checked={isSelected}
                          onCheckedChange={() => onToggleSelect(item.id)}
                          className="mt-1"
                          onClick={(e: React.MouseEvent<HTMLButtonElement>) => e.stopPropagation()}
                        />
                        )}
                      
                      <div className="flex-1 min-w-0 flex items-start gap-2">
                        {!showSelection && (
                          <ChevronRight 
                            className={`h-4 w-4 flex-shrink-0 mt-1 transition-transform ${
                              isExpanded ? 'rotate-90' : ''
                            }`}
                          />
                        )}
                        <div className="flex-1 min-w-0">
                          <h3 className="font-medium text-base break-words">
                            {item.question}
                          </h3>
                          <p className="text-sm text-muted-foreground mt-1 flex items-center gap-2">
                            {statusMeta(item.status) && (
                              <span className={`rounded-md px-1.5 py-0.5 text-xs font-medium ${statusMeta(item.status)!.className}`}>
                                {statusMeta(item.status)!.label}
                              </span>
                            )}
                            最後更新 {formatDate(item.updatedAt ?? item.createdAt)}
                          </p>
                        </div>
                      </div>

                      {!showSelection && (
                        <div className="flex gap-1 flex-shrink-0">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={(e) => handleEditStart(e, item)}
                            className="h-8 w-8 p-0"
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={(e) => handleDeleteClick(e, item.id)}
                            className="h-8 w-8 p-0 text-destructive hover:text-destructive"
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      )}
                    </div>

                    {/* 展開的內容 */}
                    {isExpanded && !showSelection && (
                      <div className="mt-4 ml-6 space-y-4" onClick={(e) => e.stopPropagation()}>
                        {isEditing ? (
                          <>
                            <div>
                              <label className="text-sm font-medium mb-2 block">問題</label>
                              <Textarea
                                value={editQuestion}
                                onChange={(e) => setEditQuestion(e.target.value)}
                                className="min-h-[80px]"
                                disabled={isSaving}
                              />
                            </div>
                            <div>
                              <label className="text-sm font-medium mb-2 block">答案</label>
                              <Textarea
                                value={editAnswer}
                                onChange={(e) => setEditAnswer(e.target.value)}
                                className="min-h-[120px]"
                                disabled={isSaving}
                              />
                            </div>
                            <div className="flex gap-2 justify-end">
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={handleEditCancel}
                                disabled={isSaving}
                              >
                                <X className="h-4 w-4 mr-1" />
                                取消
                              </Button>
                              <Button
                                size="sm"
                                onClick={() => handleEditSave(item)}
                                disabled={isSaving}
                              >
                                {isSaving ? (
                                  <Loader2 className="h-4 w-4 mr-1 animate-spin" />
                                ) : (
                                  <Check className="h-4 w-4 mr-1" />
                                )}
                                儲存
                              </Button>
                            </div>
                          </>
                        ) : (
                          <div className="bg-muted/50 rounded-lg p-4">
                            <div className="prose prose-sm max-w-none">
                              <p className="text-sm whitespace-pre-wrap break-words">
                                {item.answer}
                              </p>
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </CardContent>
                </Card>
              )
            })}
          </div>
        </ScrollArea>
      </div>

      <AlertDialog open={deleteId !== null} onOpenChange={() => setDeleteId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>確定要刪除此紀錄嗎？</AlertDialogTitle>
            <AlertDialogDescription>
              此操作無法復原，該紀錄將被永久刪除。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isDeleting}>取消</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDeleteConfirm}
              disabled={isDeleting}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {isDeleting ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  刪除中...
                </>
              ) : (
                "確定刪除"
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
