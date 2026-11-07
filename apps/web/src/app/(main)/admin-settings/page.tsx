"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import type { KnowledgeStatus, QaTypeItem as ApiQaTypeItem } from "@rag/client-sdk"
import { Button } from "@/components/ui/button"
import { AlertCircle, CheckCircle2, Loader2, RefreshCw } from "lucide-react"
import { AddQAForm, QaType, QaTypeItem } from "./AddQAForm"
import { QAList, QAPair } from "./QAList"
import { canWriteKnowledge, describeError, getClient } from "@/lib/rag"

const itemName = (item: ApiQaTypeItem) => item.nameZh || item.nameEn || item.nameOther || `細項 ${item.id}`

export default function AdminSettingsPage() {
  const router = useRouter()
  const [allowed, setAllowed] = React.useState(false)
  const [loading, setLoading] = React.useState(true)
  const [loadingQAs, setLoadingQAs] = React.useState(false)
  const [loadingTypes, setLoadingTypes] = React.useState(false)
  const [submitting, setSubmitting] = React.useState(false)
  const [qaPairs, setQaPairs] = React.useState<QAPair[]>([])
  const [qaTypes, setQaTypes] = React.useState<QaType[]>([])
  const [allItems, setAllItems] = React.useState<ApiQaTypeItem[]>([])
  const [currentQuestion, setCurrentQuestion] = React.useState("")
  const [currentAnswer, setCurrentAnswer] = React.useState("")
  const [currentType, setCurrentType] = React.useState("")
  const [currentTypeItem, setCurrentTypeItem] = React.useState("")
  const [currentStatus, setCurrentStatus] = React.useState<KnowledgeStatus>("Verified")
  const [editingId, setEditingId] = React.useState<string | null>(null)
  const [message, setMessage] = React.useState<{ type: "success" | "error"; text: string } | null>(null)

  // 顯示訊息
  const showMessage = (type: "success" | "error", text: string) => {
    setMessage({ type, text })
    setTimeout(() => setMessage(null), 3000)
  }

  // 通知 sidebar 更新
  const notifySidebarUpdate = () => {
    window.dispatchEvent(new Event('adminQAUpdated'))
  }

  // 目前所選種類下啟用中的細項
  const qaTypeItems: QaTypeItem[] = React.useMemo(
    () =>
      allItems
        .filter((item) => item.isActive && String(item.qaTypeID) === currentType)
        .map((item) => ({
          id: item.id,
          qaTypeID: item.qaTypeID,
          name: itemName(item),
          nameEn: item.nameEn || "",
          isActive: item.isActive,
        })),
    [allItems, currentType],
  )

  const loadTypes = React.useCallback(async () => {
    setLoadingTypes(true)
    try {
      const rag = getClient()
      const [types, items] = await Promise.all([rag.qaTypes.list(), rag.qaTypeItems.list()])
      setQaTypes(
        types.map((item) => ({
          id: item.id,
          name: item.nameZh || item.nameEn || item.nameOther || "未命名類型",
          description: item.nameEn || "",
        })),
      )
      setAllItems(items)
    } catch (error) {
      showMessage("error", `載入類型失敗: ${describeError(error)}`)
    } finally {
      setLoadingTypes(false)
    }
  }, [])

  // 目前 Space 的 Q&A；範圍由伺服器決定，不帶 UserID
  const loadQAPairs = React.useCallback(async () => {
    setLoadingQAs(true)
    try {
      const questions = await getClient().questions.list()
      setQaPairs(
        questions.map((q) => ({
          id: String(q.id),
          question: q.question,
          answer: q.answer,
          type: String(q.qaItemTypeID),
          qaItemTypeID: q.qaItemTypeID,
          createdAt: q.createAt,
          status: q.status as KnowledgeStatus,
        })),
      )
      notifySidebarUpdate()
    } catch (error) {
      showMessage("error", `載入失敗: ${describeError(error)}`)
    } finally {
      setLoadingQAs(false)
    }
  }, [])

  // 檢查權限並載入資料（真正的授權由伺服器判斷；這裡只決定是否顯示頁面）
  React.useEffect(() => {
    let alive = true
    canWriteKnowledge().then((ok) => {
      if (!alive) return
      if (!ok) {
        alert("您沒有權限訪問此頁面")
        router.push("/")
        return
      }
      setAllowed(true)
      Promise.all([loadTypes(), loadQAPairs()]).then(() => setLoading(false))
    })
    // 換 Space 後權限與知識庫內容都不同：重新確認，沒有權限就回首頁
    const off = getClient().on("space-changed", async () => {
      resetForm()
      if (!(await canWriteKnowledge())) {
        router.push("/")
        return
      }
      Promise.all([loadTypes(), loadQAPairs()])
    })
    return () => {
      alive = false
      off()
    }
  }, [router, loadTypes, loadQAPairs])

  const resetForm = () => {
    setEditingId(null)
    setCurrentQuestion("")
    setCurrentAnswer("")
    setCurrentType("")
    setCurrentTypeItem("")
    setCurrentStatus("Verified")
  }

  // 處理類型改變（細項已一次載入，這裡不需再呼叫 API）
  const handleTypeChange = () => {}

  // 新增或更新 Q&A
  const handleAddQA = async () => {
    if (!currentQuestion.trim() || !currentAnswer.trim() || !currentTypeItem) {
      showMessage("error", "請填寫完整的問題、答案和類型細項")
      return
    }

    setSubmitting(true)
    const isEditing = editingId !== null
    try {
      const rag = getClient()
      const base = {
        qaItemTypeID: parseInt(currentTypeItem),
        question: currentQuestion,
        answer: currentAnswer,
        status: currentStatus,
      }
      if (isEditing) {
        await rag.questions.update({ id: parseInt(editingId), ...base })
      } else {
        await rag.questions.create(base)
      }
      await loadQAPairs()
      resetForm()
      showMessage("success", isEditing ? "Q&A 已成功更新" : "Q&A 已成功新增")
    } catch (error) {
      showMessage("error", `${isEditing ? "更新" : "新增"}失敗: ${describeError(error)}`)
    } finally {
      setSubmitting(false)
    }
  }

  // 編輯 Q&A：由細項反查所屬種類，兩層選單才能正確預選
  const handleEditQA = (qa: QAPair) => {
    const item = allItems.find((i) => i.id === qa.qaItemTypeID)
    setEditingId(qa.id)
    setCurrentQuestion(qa.question)
    setCurrentAnswer(qa.answer)
    setCurrentType(item ? String(item.qaTypeID) : "")
    setCurrentTypeItem(qa.type)
    setCurrentStatus(qa.status ?? "Verified")
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  // 刪除 Q&A
  const handleDeleteQA = async (id: string) => {
    if (!confirm("確定要刪除此 Q&A 嗎？")) {
      return
    }

    try {
      await getClient().questions.delete(parseInt(id))
      await loadQAPairs()
      // 如果正在編輯被刪除的項目，清空表單
      if (editingId === id) resetForm()
      showMessage("success", "Q&A 已刪除")
    } catch (error) {
      showMessage("error", `刪除失敗: ${describeError(error)}`)
    }
  }

  // 手動重新載入
  const handleRefresh = () => {
    Promise.all([loadTypes(), loadQAPairs()])
  }

  // 根據類型細項 ID 獲取名稱
  const getTypeItemName = (itemId: string): string => {
    const item = allItems.find((i) => String(i.id) === itemId)
    return item ? itemName(item) : `細項 ${itemId}`
  }

  if (loading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="flex items-center gap-2">
          <Loader2 className="h-6 w-6 animate-spin" />
          <span className="text-lg">載入中...</span>
        </div>
      </div>
    )
  }

  if (!allowed) {
    return null
  }

  return (
    <div className="flex h-screen flex-col">
      {message && (
        <div
          className={`fixed top-4 right-4 z-50 flex items-center gap-2 rounded-lg px-4 py-3 shadow-lg ${
            message.type === "success"
              ? "bg-green-500 text-white"
              : "bg-red-500 text-white"
          }`}
        >
          {message.type === "success" ? (
            <CheckCircle2 className="h-5 w-5" />
          ) : (
            <AlertCircle className="h-5 w-5" />
          )}
          <span>{message.text}</span>
        </div>
      )}

      <div className="border-b bg-background px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold">管理設定</h1>
            <p className="text-sm text-muted-foreground mt-1">
              整理你的問答知識庫；新增與修改後立即生效
            </p>
            <p className="text-xs text-muted-foreground mt-1">
              類型: {qaTypes.length} • 細項: {allItems.length} • Q&A: {qaPairs.length}
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={handleRefresh}
            disabled={loadingQAs || loadingTypes}
          >
            {loadingQAs || loadingTypes ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="h-4 w-4" />
            )}
            <span className="ml-2">重新載入</span>
          </Button>
        </div>
      </div>

      <div className="flex-1 overflow-hidden p-6">
        <div className="grid h-full gap-6 lg:grid-cols-2">
          <AddQAForm
            currentQuestion={currentQuestion}
            setCurrentQuestion={setCurrentQuestion}
            currentAnswer={currentAnswer}
            setCurrentAnswer={setCurrentAnswer}
            currentType={currentType}
            setCurrentType={setCurrentType}
            currentTypeItem={currentTypeItem}
            setCurrentTypeItem={setCurrentTypeItem}
            currentStatus={currentStatus}
            setCurrentStatus={setCurrentStatus}
            submitting={submitting}
            onSubmit={handleAddQA}
            qaTypes={qaTypes}
            qaTypeItems={qaTypeItems}
            loadingTypes={loadingTypes}
            loadingTypeItems={false}
            editingId={editingId}
            onCancelEdit={resetForm}
            onTypeChange={handleTypeChange}
          />

          <QAList
            qaPairs={qaPairs}
            loadingQAs={loadingQAs}
            onDelete={handleDeleteQA}
            onEdit={handleEditQA}
            getTypeName={getTypeItemName}
          />
        </div>
      </div>
    </div>
  )
}
