"use client"

import * as React from "react"
import type { Draft, KnowledgeStatus, QaType, QaTypeItem } from "@rag/client-sdk"
import { AlertCircle, Check, Inbox as InboxIcon, Loader2, Pencil, X } from "lucide-react"
import { AssistantMessage } from "@/components/assistant-message"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { KNOWLEDGE_STATUSES } from "@/lib/knowledge-status"
import { canWriteKnowledge, describeError, getClient } from "@/lib/rag"

type Row = Draft & { id: number }

const itemLabel = (item: QaTypeItem) => item.nameZh || item.nameEn || item.nameOther || `細項 ${item.id}`

export default function InboxPage() {
  const [drafts, setDrafts] = React.useState<Row[]>([])
  const [types, setTypes] = React.useState<QaType[]>([])
  const [items, setItems] = React.useState<QaTypeItem[]>([])
  const [canWrite, setCanWrite] = React.useState(true)
  const [loading, setLoading] = React.useState(true)
  const [error, setError] = React.useState("")
  const [notice, setNotice] = React.useState("")
  const [busyId, setBusyId] = React.useState<number | null>(null)
  // 同步的處理中鎖：連點兩次時，第二次發生在 busyId 更新之前。伺服器也會擋（409），這裡先避免多送一次請求
  const busyRef = React.useRef(false)

  const load = React.useCallback(async () => {
    setError("")
    try {
      const rag = getClient()
      const [list, t, i, write] = await Promise.all([rag.inbox.list(), rag.qaTypes.list(), rag.qaTypeItems.list(), canWriteKnowledge()])
      setDrafts(list.filter((d): d is Row => typeof d.id === "number"))
      setTypes(t)
      setItems(i.filter((item) => item.isActive))
      setCanWrite(write)
    } catch (err) {
      setError(describeError(err))
    } finally {
      setLoading(false)
    }
  }, [])

  React.useEffect(() => {
    void load()
    // 換 Space 後，草稿與權限都不同
    return getClient().on("space-changed", () => void load())
  }, [load])

  const changed = () => window.dispatchEvent(new Event("inboxUpdated"))

  const accept = async (draft: Row, qaItemTypeID: number, status: KnowledgeStatus) => {
    if (busyRef.current) return
    busyRef.current = true
    setBusyId(draft.id)
    setError("")
    try {
      await getClient().inbox.accept(draft.id, { qaItemTypeID, status })
      setNotice("已寫入知識庫")
      setDrafts((prev) => prev.filter((d) => d.id !== draft.id))
      changed()
    } catch (err) {
      setError(describeError(err, "接受失敗"))
    } finally {
      busyRef.current = false
      setBusyId(null)
    }
  }

  const reject = async (draft: Row) => {
    if (busyRef.current) return
    busyRef.current = true
    setBusyId(draft.id)
    setError("")
    try {
      await getClient().inbox.reject(draft.id)
      setNotice("已拒絕，不會進知識庫")
      setDrafts((prev) => prev.filter((d) => d.id !== draft.id))
      changed()
    } catch (err) {
      setError(describeError(err, "拒絕失敗"))
    } finally {
      busyRef.current = false
      setBusyId(null)
    }
  }

  const save = async (draft: Row, question: string, answer: string, qaItemTypeID?: number) => {
    if (busyRef.current) return false
    busyRef.current = true
    setBusyId(draft.id)
    setError("")
    try {
      const updated = await getClient().inbox.update(draft.id, { question, answer, qaItemTypeID })
      setDrafts((prev) => prev.map((d) => (d.id === draft.id ? { ...d, ...updated, id: draft.id } : d)))
      return true
    } catch (err) {
      setError(describeError(err, "儲存失敗"))
      return false
    } finally {
      busyRef.current = false
      setBusyId(null)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4 px-4 pb-8 pt-6 md:p-8">
      <div>
        <h1 className="flex items-center gap-2 text-[28px] font-bold leading-tight tracking-tight md:text-2xl">
          <InboxIcon className="hidden h-6 w-6 md:block" />
          暫存區
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          你的 AI 或服務送來的問答會先放在這裡，還沒有進知識庫，也不會被拿來回答。你看過、接受之後才會寫入。
        </p>
      </div>

      {error && (
        <div role="alert" className="flex items-start gap-2 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-600">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}
      {notice && !error && (
        <div role="status" className="rounded border border-green-200 bg-green-50 p-3 text-sm text-green-700">
          {notice}
        </div>
      )}
      {!canWrite && !loading && (
        <div className="rounded border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          你在這個空間沒有編輯知識庫的權限，只能拒絕草稿，不能接受。
        </div>
      )}

      {loading ? (
        <div className="flex items-center gap-2 text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> 載入中...
        </div>
      ) : drafts.length === 0 ? (
        <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
          沒有待審的內容。到「開發者」建立權杖，就能讓你的 AI 把整理好的知識送到這裡。
        </p>
      ) : (
        drafts.map((draft) => (
          <DraftCard
            key={draft.id}
            draft={draft}
            types={types}
            items={items}
            canWrite={canWrite}
            busy={busyId === draft.id}
            onAccept={accept}
            onReject={reject}
            onSave={save}
          />
        ))
      )}
    </div>
  )
}

interface DraftCardProps {
  draft: Row
  types: QaType[]
  items: QaTypeItem[]
  canWrite: boolean
  busy: boolean
  onAccept: (draft: Row, qaItemTypeID: number, status: KnowledgeStatus) => void
  onReject: (draft: Row) => void
  onSave: (draft: Row, question: string, answer: string, qaItemTypeID?: number) => Promise<boolean>
}

function DraftCard({ draft, types, items, canWrite, busy, onAccept, onReject, onSave }: DraftCardProps) {
  const [editing, setEditing] = React.useState(false)
  const [question, setQuestion] = React.useState(draft.question ?? "")
  const [answer, setAnswer] = React.useState(draft.answer ?? "")
  const [itemId, setItemId] = React.useState<string>(draft.qaItemTypeID ? String(draft.qaItemTypeID) : "")
  const [status, setStatus] = React.useState<KnowledgeStatus>("Inferred")

  const saveEdit = async () => {
    if (await onSave(draft, question, answer, itemId ? Number(itemId) : undefined)) setEditing(false)
  }

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
          <span className="rounded-md bg-primary/10 px-2 py-1 font-medium text-primary">來源：{draft.source}</span>
          {draft.sourceRef && <span className="break-all">{draft.sourceRef}</span>}
          <span>{draft.createAt ? new Date(draft.createAt).toLocaleString("zh-TW") : ""}</span>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {editing ? (
          <div className="space-y-3">
            <Input value={question} onChange={(e) => setQuestion(e.target.value)} maxLength={500} aria-label="問題" />
            <Textarea value={answer} onChange={(e) => setAnswer(e.target.value)} className="min-h-40" maxLength={20000} aria-label="答案" />
          </div>
        ) : (
          <div className="space-y-2">
            <p className="font-semibold">{draft.question}</p>
            <div className="rounded-md bg-muted/50 p-3 text-sm">
              <AssistantMessage content={draft.answer ?? ""} />
            </div>
          </div>
        )}

        <div className="grid gap-3 sm:grid-cols-2">
          <label className="space-y-1 text-sm">
            <span className="font-medium">寫入哪個種類</span>
            <select value={itemId} onChange={(e) => setItemId(e.target.value)} className="h-10 w-full rounded-md border bg-background px-3 text-sm">
              <option value="">請選擇</option>
              {types.map((type) => (
                <optgroup key={type.id} label={type.nameZh || type.nameEn || `類型 ${type.id}`}>
                  {items.filter((item) => item.qaTypeID === type.id).map((item) => (
                    <option key={item.id} value={item.id}>
                      {itemLabel(item)}
                    </option>
                  ))}
                </optgroup>
              ))}
            </select>
          </label>
          <label className="space-y-1 text-sm">
            <span className="font-medium">知識狀態</span>
            <select value={status} onChange={(e) => setStatus(e.target.value as KnowledgeStatus)} className="h-10 w-full rounded-md border bg-background px-3 text-sm">
              {KNOWLEDGE_STATUSES.filter((s) => s.value !== "Superseded").map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}（{s.hint}）
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="flex flex-wrap gap-2">
          {editing ? (
            <>
              <Button size="sm" onClick={saveEdit} disabled={busy || !question.trim() || !answer.trim()}>
                儲存修改
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setEditing(false)} disabled={busy}>
                取消
              </Button>
            </>
          ) : (
            <>
              <Button size="sm" onClick={() => onAccept(draft, Number(itemId), status)} disabled={busy || !canWrite || !itemId}>
                {busy ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Check className="mr-1 h-4 w-4" />}
                接受並寫入知識庫
              </Button>
              <Button size="sm" variant="outline" onClick={() => setEditing(true)} disabled={busy || !canWrite}>
                <Pencil className="mr-1 h-4 w-4" />
                編輯
              </Button>
              <Button size="sm" variant="ghost" className="text-destructive" onClick={() => onReject(draft)} disabled={busy}>
                <X className="mr-1 h-4 w-4" />
                拒絕
              </Button>
            </>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
