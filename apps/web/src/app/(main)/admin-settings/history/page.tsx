"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { AlertCircle, CheckCircle2, Loader2 } from "lucide-react"
import { HistorySearch } from "./HistorySearch"
import { HistoryList, HistoryItem } from "./HistoryList"
import { canWriteKnowledge, describeError, getClient } from "@/lib/rag"

export default function HistoryPage() {
  const router = useRouter()
  const [allowed, setAllowed] = React.useState(false)
  const [loading, setLoading] = React.useState(true)
  const [loadingHistory, setLoadingHistory] = React.useState(false)
  const [historyItems, setHistoryItems] = React.useState<HistoryItem[]>([])
  const [filteredItems, setFilteredItems] = React.useState<HistoryItem[]>([])
  const [searchQuery, setSearchQuery] = React.useState("")
  const [message, setMessage] = React.useState<{ type: "success" | "error"; text: string } | null>(null)
  
  // 多選狀態
  const [showSelection, setShowSelection] = React.useState(false)
  const [selectedIds, setSelectedIds] = React.useState<Set<string>>(new Set())

  // 顯示訊息
  const showMessage = (type: "success" | "error", text: string) => {
    setMessage({ type, text })
    setTimeout(() => setMessage(null), 3000)
  }

  // 載入目前 Space 的知識紀錄；範圍由伺服器決定，不帶 UserID
  const loadHistory = React.useCallback(async () => {
    setLoadingHistory(true)
    try {
      const questions = await getClient().questions.list()
      const formatted: HistoryItem[] = questions.map((q) => ({
        id: String(q.id),
        question: q.question,
        answer: q.answer,
        qaItemTypeID: q.qaItemTypeID,
        status: q.status as HistoryItem["status"],
        createdAt: q.createAt,
        updatedAt: q.updateAt ?? undefined,
      }))
      setHistoryItems(formatted)
      setFilteredItems(formatted)
    } catch (error) {
      showMessage("error", `載入失敗: ${describeError(error)}`)
    } finally {
      setLoadingHistory(false)
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
      loadHistory().then(() => setLoading(false))
    })
    const off = getClient().on("space-changed", async () => {
      setSelectedIds(new Set())
      if (!(await canWriteKnowledge())) {
        router.push("/")
        return
      }
      loadHistory()
    })
    return () => {
      alive = false
      off()
    }
  }, [router, loadHistory])

  // 搜尋過濾
  React.useEffect(() => {
    if (!searchQuery.trim()) {
      setFilteredItems(historyItems)
    } else {
      const query = searchQuery.toLowerCase()
      const filtered = historyItems.filter(item => 
        item.question.toLowerCase().includes(query) ||
        item.answer.toLowerCase().includes(query)
      )
      setFilteredItems(filtered)
    }
  }, [searchQuery, historyItems])

  // 切換多選模式
  const handleToggleSelection = () => {
    setShowSelection(!showSelection)
    if (showSelection) {
      setSelectedIds(new Set())
    }
  }

  // 切換單個項目選擇
  const handleToggleSelect = (id: string) => {
    setSelectedIds(prev => {
      const newSet = new Set(prev)
      if (newSet.has(id)) {
        newSet.delete(id)
      } else {
        newSet.add(id)
      }
      return newSet
    })
  }

  // 全選/取消全選
  const handleSelectAll = (checked: boolean) => {
    if (checked) {
      setSelectedIds(new Set(filteredItems.map(item => item.id)))
    } else {
      setSelectedIds(new Set())
    }
  }

  // 刪除選中的項目
  const handleDeleteSelected = async () => {
    if (selectedIds.size === 0) return

    if (!confirm(`確定要刪除選中的 ${selectedIds.size} 筆紀錄嗎？`)) {
      return
    }

    const ids = Array.from(selectedIds)
    const rag = getClient()
    const results = await Promise.allSettled(ids.map((id) => rag.questions.delete(parseInt(id))))
    const failed = results.filter((r) => r.status === "rejected")

    await loadHistory()
    // 失敗的項目保持勾選，方便重試
    setSelectedIds(new Set(ids.filter((_, i) => results[i].status === "rejected")))
    if (failed.length === 0) {
      setShowSelection(false)
      showMessage("success", `已刪除 ${ids.length} 筆紀錄`)
    } else {
      const reason = (failed[0] as PromiseRejectedResult).reason
      showMessage("error", `${failed.length} 筆刪除失敗: ${describeError(reason)}`)
    }
  }

  // 刪除單個歷史紀錄
  const handleDelete = async (id: string) => {
    if (!confirm("確定要刪除此紀錄嗎？")) {
      return
    }

    try {
      await getClient().questions.delete(parseInt(id))
      await loadHistory()
      showMessage("success", "紀錄已刪除")
    } catch (error) {
      showMessage("error", `刪除失敗: ${describeError(error)}`)
    }
  }

  // 編輯歷史紀錄（種類細項與知識狀態維持原值）
  const handleEdit = async (item: HistoryItem, newQuestion: string, newAnswer: string) => {
    try {
      await getClient().questions.update({
        id: parseInt(item.id),
        qaItemTypeID: item.qaItemTypeID ?? 0,
        question: newQuestion,
        answer: newAnswer,
        status: item.status,
      })
      await loadHistory()
      showMessage("success", "紀錄已更新")
    } catch (error) {
      showMessage("error", `更新失敗: ${describeError(error)}`)
    }
  }

  // 新增對話
  const handleNewChat = () => {
    router.push("/admin-settings")
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

      <div className="flex-1 overflow-hidden p-6">
        <div className="h-full flex flex-col gap-4 max-w-5xl mx-auto">
          <HistorySearch 
            searchQuery={searchQuery}
            onSearchChange={setSearchQuery}
            totalCount={historyItems.length}
            filteredCount={filteredItems.length}
            onNewClick={handleNewChat}
            selectedCount={selectedIds.size}
            showSelection={showSelection}
            onToggleSelection={handleToggleSelection}
            onSelectAll={handleSelectAll}
            onDeleteSelected={handleDeleteSelected}
          />

          <HistoryList
            items={filteredItems}
            loading={loadingHistory}
            onDelete={handleDelete}
            onEdit={handleEdit}
            showSelection={showSelection}
            selectedIds={selectedIds}
            onToggleSelect={handleToggleSelect}
          />
        </div>
      </div>
    </div>
  )
}
