"use client"

import * as React from "react"
import type { ApiToken, CreatedApiToken, Space } from "@rag/client-sdk"
import { AlertCircle, Check, Copy, KeyRound, Loader2, Plus, Trash2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
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
import { API_BASE_URL, describeError, getClient } from "@/lib/rag"
import { skillTemplate } from "./skill-template"
import { WebhooksSection } from "./webhooks"

const SCOPES = [
  { value: "knowledge.search", label: "搜尋知識庫", hint: "只讀，取得檢索到的內容" },
  { value: "inbox.write", label: "放進暫存區", hint: "只能放到暫存區，你審查接受後才會成為知識" },
  { value: "space.read", label: "查詢空間", hint: "列出可使用的空間" },
] as const

const EXPIRIES = [
  { value: "30", label: "30 天" },
  { value: "90", label: "90 天" },
  { value: "365", label: "1 年" },
  { value: "", label: "不過期" },
]

function CopyButton({ text, label = "複製" }: { text: string; label?: string }) {
  const [copied, setCopied] = React.useState(false)
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* 剪貼簿被封鎖時不做任何事 */
    }
  }
  return (
    <Button type="button" variant="outline" size="sm" onClick={copy}>
      {copied ? <Check className="mr-1 h-4 w-4" /> : <Copy className="mr-1 h-4 w-4" />}
      {copied ? "已複製" : label}
    </Button>
  )
}

const formatDate = (value?: string | null) => (value ? new Date(value).toLocaleDateString("zh-TW") : "—")

export default function DeveloperPage() {
  const [tokens, setTokens] = React.useState<ApiToken[]>([])
  const [spaces, setSpaces] = React.useState<Space[]>([])
  const [loading, setLoading] = React.useState(true)
  const [error, setError] = React.useState("")
  const [creating, setCreating] = React.useState(false)
  const creatingRef = React.useRef(false) // 同步鎖：連點「建立權杖」不會建出兩把
  const [created, setCreated] = React.useState<CreatedApiToken | null>(null)
  const [revoking, setRevoking] = React.useState<ApiToken | null>(null)

  const [name, setName] = React.useState("")
  const [scopes, setScopes] = React.useState<string[]>(["knowledge.search", "inbox.write"])
  const [limitSpaces, setLimitSpaces] = React.useState(false)
  const [spaceIds, setSpaceIds] = React.useState<number[]>([])
  const [expiry, setExpiry] = React.useState("90")

  const baseUrl = API_BASE_URL || (typeof window === "undefined" ? "" : window.location.origin)

  const load = React.useCallback(async () => {
    try {
      const rag = getClient()
      const [list, mine] = await Promise.all([rag.apiTokens.list(), rag.spaces.list()])
      setTokens(list)
      setSpaces(mine)
    } catch (err) {
      setError(describeError(err))
    } finally {
      setLoading(false)
    }
  }, [])

  React.useEffect(() => {
    void load()
  }, [load])

  const toggle = <T,>(list: T[], value: T): T[] => (list.includes(value) ? list.filter((v) => v !== value) : [...list, value])

  const create = async (e: React.FormEvent) => {
    e.preventDefault()
    if (creatingRef.current) return
    creatingRef.current = true
    setError("")
    setCreating(true)
    try {
      const result = await getClient().apiTokens.create({
        name: name.trim(),
        scopes,
        spaceIDs: limitSpaces && spaceIds.length > 0 ? spaceIds : undefined,
        expiresInDays: expiry ? Number(expiry) : undefined,
      })
      setCreated(result)
      setName("")
      await load()
    } catch (err) {
      setError(describeError(err, "建立權杖失敗"))
    } finally {
      creatingRef.current = false
      setCreating(false)
    }
  }

  const revoke = async () => {
    const token = revoking
    setRevoking(null)
    if (!token || token.id === undefined) return
    try {
      await getClient().apiTokens.revoke(token.id)
      if (created?.id === token.id) setCreated(null)
      await load()
    } catch (err) {
      setError(describeError(err, "撤銷失敗"))
    }
  }

  const spaceName = (id: number) => spaces.find((s) => s.spaceID === id)?.name ?? `空間 ${id}`
  const selectableSpaces = spaces.filter((s): s is Space & { spaceID: number } => typeof s.spaceID === "number")
  const canCreate = name.trim().length > 0 && scopes.length > 0 && (!limitSpaces || spaceIds.length > 0)

  return (
    <div className="mx-auto max-w-3xl space-y-6 px-4 pb-8 pt-4 md:p-8">
      <div>
        <h1 className="hidden text-2xl font-bold md:block">開發者</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          用應用程式權杖，讓你自己的 AI 或服務搜尋知識庫，並把整理好的內容放進暫存區。權杖不能直接寫入知識庫，一定由你審查接受。
        </p>
      </div>

      {error && (
        <div role="alert" className="flex items-start gap-2 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-600">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {created && (
        <Card className="border-primary">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <KeyRound className="h-5 w-5" />
              請立刻複製你的權杖
            </CardTitle>
            <CardDescription>這是唯一一次會顯示完整權杖，離開或重新整理後就看不到了。請放進環境變數（例如 RAG_TOKEN），不要貼到對話或程式碼裡。</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <code className="block break-all rounded-md bg-muted p-3 text-sm">{created.token ?? ""}</code>
            <div className="flex gap-2">
              <CopyButton text={created.token ?? ""} label="複製權杖" />
              <Button type="button" variant="ghost" size="sm" onClick={() => setCreated(null)}>
                我已保存
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>建立權杖</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={create} className="space-y-5">
            <div className="space-y-2">
              <Label htmlFor="token-name">名稱</Label>
              <Input id="token-name" value={name} onChange={(e) => setName(e.target.value)} maxLength={50} placeholder="例如：我的筆記網站" disabled={creating} />
            </div>

            <fieldset className="space-y-2">
              <legend className="text-sm font-medium">權限</legend>
              {SCOPES.map((scope) => (
                <label key={scope.value} className="flex cursor-pointer items-start gap-3 rounded-md border p-3">
                  <Checkbox checked={scopes.includes(scope.value)} onCheckedChange={() => setScopes(toggle(scopes, scope.value))} className="mt-0.5" />
                  <span>
                    <span className="block text-sm font-medium">{scope.label}</span>
                    <span className="block text-xs text-muted-foreground">{scope.hint}</span>
                  </span>
                </label>
              ))}
            </fieldset>

            <fieldset className="space-y-2">
              <legend className="text-sm font-medium">可使用的空間</legend>
              <label className="flex items-center gap-2 text-sm">
                <input type="radio" checked={!limitSpaces} onChange={() => setLimitSpaces(false)} /> 我所有的空間
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input type="radio" checked={limitSpaces} onChange={() => setLimitSpaces(true)} /> 只限這幾個
              </label>
              {limitSpaces && (
                <div className="space-y-1 pl-6">
                  {selectableSpaces.map((space) => (
                    <label key={space.spaceID} className="flex items-center gap-2 text-sm">
                      <Checkbox checked={spaceIds.includes(space.spaceID)} onCheckedChange={() => setSpaceIds(toggle(spaceIds, space.spaceID))} />
                      {space.name}
                    </label>
                  ))}
                </div>
              )}
            </fieldset>

            <div className="space-y-2">
              <Label htmlFor="token-expiry">有效期限</Label>
              <select
                id="token-expiry"
                value={expiry}
                onChange={(e) => setExpiry(e.target.value)}
                className="h-10 w-full rounded-md border bg-background px-3 text-sm md:w-48"
              >
                {EXPIRIES.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            <Button type="submit" disabled={!canCreate || creating}>
              {creating ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Plus className="mr-2 h-4 w-4" />}
              建立權杖
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>我的權杖</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" /> 載入中...
            </div>
          ) : tokens.length === 0 ? (
            <p className="text-sm text-muted-foreground">還沒有任何權杖。</p>
          ) : (
            <ul className="divide-y">
              {tokens.map((token) => (
                <li key={token.id} className="flex items-start justify-between gap-3 py-3">
                  <div className="min-w-0 space-y-1">
                    <p className="font-medium">{token.name}</p>
                    <p className="text-xs text-muted-foreground">
                      <code>{token.tokenPrefix}…</code> · {(token.scopes ?? []).map((s) => SCOPES.find((x) => x.value === s)?.label ?? s).join("、")}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      空間：{!token.spaceIDs?.length ? "全部" : token.spaceIDs.map(spaceName).join("、")} · 建立 {formatDate(token.createAt)} · 到期 {token.expiresAt ? formatDate(token.expiresAt) : "不過期"} · 最近使用 {formatDate(token.lastUsedAt)}
                    </p>
                  </div>
                  <Button variant="ghost" size="icon" className="text-destructive" onClick={() => setRevoking(token)} aria-label={`撤銷 ${token.name}`}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <WebhooksSection />

      <Card>
        <CardHeader>
          <CardTitle>給你的 AI 用的 Skill</CardTitle>
          <CardDescription>
            把這份存成你機器上的 Skill（例如 Claude Code 的 SKILL.md）。平台只負責存放與檢索，整理與判斷交給你自己的 AI；它放進暫存區的內容，你審查接受後才會成為知識。
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <CopyButton text={skillTemplate(baseUrl)} label="複製 Skill 範本" />
          <pre className="max-h-72 overflow-auto rounded-md bg-muted p-3 text-xs">{skillTemplate(baseUrl)}</pre>
        </CardContent>
      </Card>

      <AlertDialog open={revoking !== null} onOpenChange={(open) => !open && setRevoking(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>撤銷這把權杖？</AlertDialogTitle>
            <AlertDialogDescription>「{revoking?.name}」會立刻失效，使用它的 AI 或服務都會被拒絕，無法復原。</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction onClick={revoke}>撤銷</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
