"use client"

import * as React from "react"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Save, Plus, Loader2, X } from "lucide-react"
import type { KnowledgeStatus } from "@rag/client-sdk"
import { KNOWLEDGE_STATUSES } from "@/lib/knowledge-status"

export interface QaType {
  id: number
  name: string
  description?: string
}

export interface QaTypeItem {
  id: number
  qaTypeID: number
  name: string
  nameEn?: string
  isActive?: boolean
}

interface AddQAFormProps {
  currentQuestion: string
  setCurrentQuestion: (value: string) => void
  currentAnswer: string
  setCurrentAnswer: (value: string) => void
  currentType: string
  setCurrentType: (value: string) => void
  currentTypeItem: string
  setCurrentTypeItem: (value: string) => void
  currentStatus: KnowledgeStatus
  setCurrentStatus: (value: KnowledgeStatus) => void
  submitting: boolean
  onSubmit: () => void
  qaTypes: QaType[]
  qaTypeItems: QaTypeItem[]
  loadingTypes: boolean
  loadingTypeItems: boolean
  editingId: string | null
  onCancelEdit: () => void
  onTypeChange: (typeId: string) => void
  /** 手機版全螢幕編輯面板內使用：只輸出欄位，不含卡片外框、標題與送出按鈕（由面板自己提供） */
  chromeless?: boolean
}

export function AddQAForm({
  currentQuestion,
  setCurrentQuestion,
  currentAnswer,
  setCurrentAnswer,
  currentType,
  setCurrentType,
  currentTypeItem,
  setCurrentTypeItem,
  currentStatus,
  setCurrentStatus,
  submitting,
  onSubmit,
  qaTypes,
  qaTypeItems,
  loadingTypes,
  loadingTypeItems,
  editingId,
  onCancelEdit,
  onTypeChange,
  chromeless = false,
}: AddQAFormProps) {
  const isEditing = editingId !== null

  const handleTypeChange = (value: string) => {
    setCurrentType(value)
    setCurrentTypeItem("") // 清空細項選擇
    onTypeChange(value) // 觸發載入細項
  }

  const fields = (
    <>
        {/* 第一層：類型 */}
        <div className="space-y-2">
          <Label htmlFor="type">類型</Label>
          {loadingTypes ? (
            <div className="flex items-center gap-2 h-10 px-3 border rounded-md">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-sm text-muted-foreground">載入類型中...</span>
            </div>
          ) : (
            <Select 
              value={currentType} 
              onValueChange={handleTypeChange}
              disabled={submitting}
            >
              <SelectTrigger id="type">
                <SelectValue placeholder="選擇類型" />
              </SelectTrigger>
              <SelectContent>
                {qaTypes.length === 0 ? (
                  <div className="px-2 py-1.5 text-sm text-muted-foreground">
                    無可用類型
                  </div>
                ) : (
                  qaTypes.map((type) => (
                    <SelectItem key={type.id} value={type.id.toString()}>
                      <span className="font-medium">{type.name}</span>
                      {type.description && (
                        <span className="text-xs text-muted-foreground ml-2">
                          ({type.description})
                        </span>
                      )}
                    </SelectItem>
                  ))
                )}
              </SelectContent>
            </Select>
          )}
        </div>

        {/* 第二層：類型細項 */}
        <div className="space-y-2">
          <Label htmlFor="typeItem">類型細項</Label>
          {!currentType ? (
            <div className="flex items-center gap-2 h-10 px-3 border rounded-md bg-muted">
              <span className="text-sm text-muted-foreground">請先選擇類型</span>
            </div>
          ) : loadingTypeItems ? (
            <div className="flex items-center gap-2 h-10 px-3 border rounded-md">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-sm text-muted-foreground">載入細項中...</span>
            </div>
          ) : (
            <>
              <Select 
                value={currentTypeItem} 
                onValueChange={setCurrentTypeItem}
                disabled={submitting || !currentType}
              >
                <SelectTrigger id="typeItem">
                  <SelectValue placeholder="選擇細項" />
                </SelectTrigger>
                <SelectContent>
                  {qaTypeItems.length === 0 ? (
                    <div className="px-2 py-1.5 text-sm text-muted-foreground">
                      此類型下無可用細項
                    </div>
                  ) : (
                    qaTypeItems.map((item) => (
                      <SelectItem key={item.id} value={item.id.toString()}>
                        <span className="font-medium">{item.name}</span>
                        {item.nameEn && (
                          <span className="text-xs text-muted-foreground ml-2">
                            ({item.nameEn})
                          </span>
                        )}
                      </SelectItem>
                    ))
                  )}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {currentTypeItem ? `已選擇細項 ID: ${currentTypeItem}` : '請選擇細項'}
              </p>
            </>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="status">知識狀態</Label>
          <Select
            value={currentStatus}
            onValueChange={(value) => setCurrentStatus(value as KnowledgeStatus)}
            disabled={submitting}
          >
            <SelectTrigger id="status">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {KNOWLEDGE_STATUSES.map((status) => (
                <SelectItem key={status.value} value={status.value}>
                  <span className="font-medium">{status.label}</span>
                  <span className="text-xs text-muted-foreground ml-2">{status.hint}</span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="question">問題 (Q)</Label>
          <Textarea
            id="question"
            placeholder="輸入問題內容..."
            value={currentQuestion}
            onChange={(e) => setCurrentQuestion(e.target.value)}
            rows={4}
            className="resize-none"
            disabled={submitting}
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="answer">答案 (A)</Label>
          <Textarea
            id="answer"
            placeholder="輸入答案內容...（支援 Markdown：標題、清單、表格、程式碼區塊）"
            value={currentAnswer}
            onChange={(e) => setCurrentAnswer(e.target.value)}
            rows={6}
            className="resize-none"
            disabled={submitting}
          />
        </div>

    </>
  )

  if (chromeless) return <div className="space-y-5">{fields}</div>

  return (
    <Card className="flex flex-col">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2">
            {isEditing ? (
              <>
                <Save className="h-5 w-5" />
                編輯 Q&A
              </>
            ) : (
              <>
                <Plus className="h-5 w-5" />
                新增 Q&A
              </>
            )}
          </CardTitle>
          {isEditing && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onCancelEdit}
              disabled={submitting}
            >
              <X className="h-4 w-4 mr-1" />
              取消編輯
            </Button>
          )}
        </div>
        <CardDescription>
          {isEditing
            ? "修改問答的內容，儲存後立即生效"
            : "建立新的問答對，儲存後即可被檢索"}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex-1 space-y-4">
        {fields}

        <Button 
          onClick={onSubmit} 
          className="w-full"
          size="lg"
          disabled={submitting || loadingTypes || !currentTypeItem}
        >
          {submitting ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              {isEditing ? "更新中..." : "儲存中..."}
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              {isEditing ? "更新 Q&A" : "儲存 Q&A"}
            </>
          )}
        </Button>
      </CardContent>
    </Card>
  )
}
