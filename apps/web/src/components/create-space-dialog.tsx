"use client"

import * as React from "react"
import { Loader2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { describeError, getClient } from "@/lib/rag"

/** 空間清單有變動（建立、重新命名）：側邊欄與「我的」頁據此重新載入 */
export const SPACES_UPDATED_EVENT = "spacesUpdated"

/**
 * 建立新空間。你是它的擁有者；每個空間的知識庫、對話與暫存區互相獨立。
 * 建立後自動切換到新空間。選擇只是 hint，授權由伺服器決定。
 */
export function CreateSpaceDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const [name, setName] = React.useState("")
  const [error, setError] = React.useState("")
  const [creating, setCreating] = React.useState(false)
  const busyRef = React.useRef(false) // 同步鎖：連點「建立」不會建出兩個

  React.useEffect(() => {
    if (open) {
      setName("")
      setError("")
    }
  }, [open])

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    const trimmed = name.trim()
    if (!trimmed || busyRef.current) return
    busyRef.current = true
    setCreating(true)
    setError("")
    try {
      const rag = getClient()
      const created = await rag.spaces.create(trimmed)
      window.dispatchEvent(new Event(SPACES_UPDATED_EVENT))
      onOpenChange(false)
      if (created.spaceID != null) rag.spaces.select(created.spaceID)
    } catch (err) {
      setError(describeError(err, "建立空間失敗"))
    } finally {
      busyRef.current = false
      setCreating(false)
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={(next) => !creating && onOpenChange(next)}>
      <AlertDialogContent>
        <form onSubmit={submit} className="space-y-4">
          <AlertDialogHeader>
            <AlertDialogTitle>建立新空間</AlertDialogTitle>
            <AlertDialogDescription>每個空間有自己的知識庫、對話與暫存區，互相獨立。你是這個空間的擁有者。</AlertDialogDescription>
          </AlertDialogHeader>
          <div className="space-y-2">
            <Label htmlFor="space-name">空間名稱</Label>
            <Input id="space-name" value={name} onChange={(e) => setName(e.target.value)} maxLength={100} placeholder="例如：研究筆記、團隊 A" autoFocus disabled={creating} />
            {error && (
              <p role="alert" className="text-sm text-red-600">
                {error}
              </p>
            )}
          </div>
          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={creating}>
              取消
            </Button>
            <Button type="submit" disabled={!name.trim() || creating}>
              {creating && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              建立
            </Button>
          </div>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  )
}
