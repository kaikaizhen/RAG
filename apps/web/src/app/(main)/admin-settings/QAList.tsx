"use client"

import * as React from "react"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Trash2, Loader2, Pencil } from "lucide-react"
import type { KnowledgeStatus } from "@rag/client-sdk"
import { statusMeta } from "@/lib/knowledge-status"

export interface QAPair {
  id: string
  question: string
  answer: string
  type: string
  createdAt: string
  qaItemTypeID?: number
  status?: KnowledgeStatus
}

interface QAListProps {
  qaPairs: QAPair[]
  loadingQAs: boolean
  onDelete: (id: string) => void
  onEdit: (qa: QAPair) => void
  getTypeName?: (typeId: string) => string
}

export function QAList({ qaPairs, loadingQAs, onDelete, onEdit, getTypeName }: QAListProps) {
  // 如果沒有提供 getTypeName，使用預設實作
  const defaultGetTypeName = (typeId: string): string => {
    return `類型 ${typeId}`
  }

  const displayTypeName = getTypeName || defaultGetTypeName

  return (
    <Card className="flex flex-col lg:h-full lg:max-h-[600px]">
      <CardHeader className="flex-shrink-0">
      <CardTitle>已儲存的 Q&A ({qaPairs.length})</CardTitle>
      <CardDescription>
        目前空間的所有問答
      </CardDescription>
      </CardHeader>
      <CardContent className="p-0 lg:flex-1 lg:overflow-hidden">
      <ScrollArea className="w-full lg:h-full lg:max-h-[500px]">
        <div className="px-4 pb-4 md:px-6">
        {loadingQAs ? (
          <div className="flex h-[200px] items-center justify-center">
          <Loader2 className="h-6 w-6 animate-spin" />
          <span className="ml-2">載入中...</span>
          </div>
        ) : qaPairs.length === 0 ? (
          <div className="flex h-[200px] items-center justify-center text-muted-foreground">
          目前沒有任何 Q&A
          </div>
        ) : (
          <div className="space-y-4">
          {qaPairs.map((qa) => (
            <Card key={qa.id} className="border-2">
            <CardHeader className="pb-3">
              <div className="flex items-start justify-between">
              <div className="flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                <span className="rounded-md bg-primary/10 px-2 py-1 text-xs font-medium text-primary">
                  {displayTypeName(qa.type)}
                </span>
                {statusMeta(qa.status) && (
                  <span className={`rounded-md px-2 py-1 text-xs font-medium ${statusMeta(qa.status)!.className}`}>
                    {statusMeta(qa.status)!.label}
                  </span>
                )}
                <span className="text-xs text-muted-foreground">
                  {new Date(qa.createdAt).toLocaleDateString("zh-TW")}
                </span>
                </div>
              </div>
              <div className="flex gap-1">
                <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 flex-shrink-0 text-blue-600 hover:bg-blue-50 hover:text-blue-700"
                onClick={() => onEdit(qa)}
                title="編輯"
                >
                <Pencil className="h-4 w-4" />
                </Button>
                <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 flex-shrink-0 text-destructive hover:bg-destructive/10 hover:text-destructive"
                onClick={() => onDelete(qa.id)}
                title="刪除"
                >
                <Trash2 className="h-4 w-4" />
                </Button>
              </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              <div>
              <div className="mb-1 text-sm font-semibold text-foreground/70">
                問題：
              </div>
              <p className="text-sm leading-relaxed whitespace-pre-wrap break-words">
                {qa.question}
              </p>
              </div>
              <div>
              <div className="mb-1 text-sm font-semibold text-foreground/70">
                答案：
              </div>
              <p className="text-sm leading-relaxed text-muted-foreground whitespace-pre-wrap break-words">
                {qa.answer}
              </p>
              </div>
            </CardContent>
            </Card>
          ))}
          </div>
        )}
        </div>
      </ScrollArea>
      </CardContent>
    </Card>
  )
}
