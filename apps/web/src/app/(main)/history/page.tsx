"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { Loader2, MessageSquare, Plus } from "lucide-react"
import type { ChatSummary } from "@rag/client-sdk"
import { Button } from "@/components/ui/button"
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
import { MobileGroup, MobileTitle } from "@/components/mobile/list"
import { useLongPress } from "@/hooks/use-long-press"
import { describeError, getClient } from "@/lib/rag"

type Chat = ChatSummary & { id: string; name: string }

/**
 * 歷史對話（手機版的分頁；桌面版在側邊欄）。點一下開啟、長按刪除。
 * 開啟時帶 ?chat=<編號> 回到問答頁，由問答頁載入那個對話。
 */
export default function HistoryPage() {
  const router = useRouter()
  const [chats, setChats] = React.useState<Chat[]>([])
  const [loading, setLoading] = React.useState(true)
  const [error, setError] = React.useState("")
  const [pendingDelete, setPendingDelete] = React.useState<Chat | null>(null)

  const load = React.useCallback(async () => {
    try {
      const list = await getClient().chat.history()
      setChats(list.filter((chat): chat is Chat => !!chat.id && !!chat.name))
      setError("")
    } catch (err) {
      setError(describeError(err))
    } finally {
      setLoading(false)
    }
  }, [])

  React.useEffect(() => {
    void load()
    return getClient().on("space-changed", () => void load())
  }, [load])

  const confirmDelete = async () => {
    const chat = pendingDelete
    setPendingDelete(null)
    if (!chat) return
    try {
      await getClient().chat.delete(chat.id)
      setChats((prev) => prev.filter((c) => c.id !== chat.id))
      window.dispatchEvent(new CustomEvent("chatDeleted", { detail: { chatId: chat.id } }))
    } catch (err) {
      setError(describeError(err, "刪除失敗"))
    }
  }

  return (
    <div className="mx-auto max-w-2xl px-4 pb-8 pt-6 md:p-8">
      <MobileTitle title="歷史對話">
        <Button size="icon" variant="outline" className="h-11 w-11 rounded-full" onClick={() => router.push("/")} aria-label="新對話">
          <Plus className="h-5 w-5" />
        </Button>
      </MobileTitle>
      <h1 className="mb-5 hidden text-2xl font-bold md:block">歷史對話</h1>

      {error && (
        <p role="alert" className="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-600">
          {error}
        </p>
      )}

      {loading ? (
        <div className="flex justify-center py-16 text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
        </div>
      ) : chats.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-20 text-center text-muted-foreground">
          <MessageSquare className="h-10 w-10" />
          <p>還沒有對話，到「問答」開始提問吧。</p>
        </div>
      ) : (
        <>
          <MobileGroup label={`${chats.length} 個對話・長按可刪除`}>
            {chats.map((chat) => (
              <ChatRow key={chat.id} chat={chat} onOpen={() => router.push(`/?chat=${encodeURIComponent(chat.id)}`)} onDelete={setPendingDelete} />
            ))}
          </MobileGroup>
        </>
      )}

      <AlertDialog open={pendingDelete !== null} onOpenChange={(open) => !open && setPendingDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>刪除這個對話？</AlertDialogTitle>
            <AlertDialogDescription className="break-words">「{pendingDelete?.name}」的所有訊息都會被刪除，無法復原。</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction onClick={confirmDelete}>刪除</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function ChatRow({ chat, onOpen, onDelete }: { chat: Chat; onOpen: () => void; onDelete: (chat: Chat) => void }) {
  const longPress = useLongPress(() => onDelete(chat))
  return (
    <button
      type="button"
      {...longPress}
      onClick={onOpen}
      className="flex min-h-14 w-full select-none items-center gap-3 px-4 py-3 text-left transition-colors [-webkit-touch-callout:none] active:bg-muted"
    >
      <MessageSquare className="h-5 w-5 shrink-0 text-muted-foreground" />
      <span className="min-w-0 flex-1 truncate font-medium">{chat.name}</span>
    </button>
  )
}
