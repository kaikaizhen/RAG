"use client"

import * as React from "react"
import type { CreatedWebhook, Webhook, WebhookDelivery, WebhookEvent } from "@rag/client-sdk"
import { AlertCircle, Check, ChevronDown, ChevronRight, Copy, Loader2, Plus, RotateCw, Send, Trash2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { describeError, getClient } from "@/lib/rag"

const EVENTS: { value: WebhookEvent; label: string }[] = [
  { value: "draft.created", label: "暫存區收到新草稿" },
  { value: "draft.accepted", label: "草稿被接受（已寫入知識庫）" },
  { value: "draft.rejected", label: "草稿被拒絕" },
]

const DELIVERY_STATUS: Record<string, { label: string; className: string }> = {
  Succeeded: { label: "已送達", className: "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300" },
  Pending: { label: "等待重試", className: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300" },
  Dead: { label: "送不出去", className: "bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300" },
}

const formatTime = (value?: string | null) => (value ? new Date(value + (value.endsWith("Z") ? "" : "Z")).toLocaleString("zh-TW") : "—")

/** 對方驗證簽章的範例（Node.js）；放在說明裡，讓接 webhook 的人直接複製 */
const VERIFY_EXAMPLE = `import { createHmac, timingSafeEqual } from "node:crypto"

// body 要用「原始的請求內容字串」，不要先 JSON.parse 再轉回字串
export function verify(secret, headers, body) {
  const ts = headers["x-rag-timestamp"]
  if (Math.abs(Date.now() / 1000 - Number(ts)) > 300) return false // 超過 5 分鐘：當成重放
  const expected = "sha256=" + createHmac("sha256", secret).update(\`\${ts}.\${body}\`).digest("hex")
  const given = headers["x-rag-signature"] ?? ""
  return given.length === expected.length && timingSafeEqual(Buffer.from(given), Buffer.from(expected))
}
// 用 payload.id 去重：自動重試與手動重送都是同一個 id，同一個 id 只處理一次。
// 收到後盡快回 2xx（10 秒內），耗時的事情自己排隊處理。`

export function WebhooksSection() {
  const [hooks, setHooks] = React.useState<Webhook[]>([])
  const [loading, setLoading] = React.useState(true)
  const [error, setError] = React.useState("")
  const [created, setCreated] = React.useState<CreatedWebhook | null>(null)
  const [copied, setCopied] = React.useState(false)

  const [name, setName] = React.useState("")
  const [url, setUrl] = React.useState("")
  const [events, setEvents] = React.useState<WebhookEvent[]>(["draft.accepted"])
  const [creating, setCreating] = React.useState(false)
  const busyRef = React.useRef(false) // 同步鎖：連點建立 / 測試 / 重送 / 刪除都不會重複送出

  const [openId, setOpenId] = React.useState<number | null>(null)
  const [deliveries, setDeliveries] = React.useState<WebhookDelivery[]>([])
  const [loadingDeliveries, setLoadingDeliveries] = React.useState(false)
  const [workingId, setWorkingId] = React.useState<string | null>(null)
  const [notice, setNotice] = React.useState("")

  const load = React.useCallback(async () => {
    try {
      setHooks(await getClient().webhooks.list())
    } catch (err) {
      setError(describeError(err))
    } finally {
      setLoading(false)
    }
  }, [])

  React.useEffect(() => {
    void load()
  }, [load])

  const loadDeliveries = async (id: number) => {
    setLoadingDeliveries(true)
    try {
      setDeliveries(await getClient().webhooks.deliveries(id))
    } catch (err) {
      setError(describeError(err))
    } finally {
      setLoadingDeliveries(false)
    }
  }

  /** 包一層：同步上鎖、清訊息、統一錯誤處理 */
  const run = async (key: string, action: () => Promise<void>) => {
    if (busyRef.current) return
    busyRef.current = true
    setWorkingId(key)
    setError("")
    setNotice("")
    try {
      await action()
    } catch (err) {
      setError(describeError(err))
    } finally {
      busyRef.current = false
      setWorkingId(null)
    }
  }

  const create = async (e: React.FormEvent) => {
    e.preventDefault()
    if (busyRef.current) return
    busyRef.current = true
    setCreating(true)
    setError("")
    try {
      const result = await getClient().webhooks.create({ name: name.trim(), url: url.trim(), events })
      setCreated(result)
      setName("")
      setUrl("")
      await load()
    } catch (err) {
      setError(describeError(err, "建立失敗"))
    } finally {
      busyRef.current = false
      setCreating(false)
    }
  }

  const test = (hook: Webhook) =>
    run(`test-${hook.id}`, async () => {
      const result = await getClient().webhooks.test(hook.id!)
      setNotice(result.status === "Succeeded" ? "測試事件已送達" : `測試事件沒有送達：${result.lastError ?? "未知原因"}`)
      if (openId === hook.id) await loadDeliveries(hook.id!)
      await load()
    })

  const toggle = (hook: Webhook) =>
    run(`toggle-${hook.id}`, async () => {
      await getClient().webhooks.update(hook.id!, { name: hook.name ?? "", url: hook.url ?? "", events: hook.events ?? [], isActive: !hook.isActive })
      await load()
    })

  const remove = (hook: Webhook) => {
    if (!confirm(`確定要刪除「${hook.name}」嗎？送出紀錄也會一併刪除。`)) return
    return run(`delete-${hook.id}`, async () => {
      await getClient().webhooks.delete(hook.id!)
      if (openId === hook.id) setOpenId(null)
      await load()
    })
  }

  const redeliver = (hook: Webhook, delivery: WebhookDelivery) =>
    run(`redeliver-${delivery.id}`, async () => {
      const result = await getClient().webhooks.redeliver(hook.id!, delivery.id!)
      setNotice(result.status === "Succeeded" ? "已重新送達" : `重送失敗：${result.lastError ?? "未知原因"}`)
      await loadDeliveries(hook.id!)
      await load()
    })

  const toggleOpen = async (hook: Webhook) => {
    if (openId === hook.id) return setOpenId(null)
    setOpenId(hook.id!)
    setDeliveries([])
    await loadDeliveries(hook.id!)
  }

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* 剪貼簿被封鎖時不做任何事 */
    }
  }

  const httpsOnly = url.trim() === "" || /^https:\/\//i.test(url.trim())
  const canCreate = name.trim() !== "" && url.trim() !== "" && httpsOnly && events.length > 0

  return (
    <Card>
      <CardHeader>
        <CardTitle>Webhook</CardTitle>
        <CardDescription>
          事件發生時，伺服器會用簽章的 HTTPS 請求通知你的網址，讓你把功能接到自己的服務。網址一律要 https://，而且必須是公開網路上的位址（不能是 localhost 或內網）；開發時可以用 ngrok、cloudflared 這類通道取得 https 網址。
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
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

        {created && (
          <div className="space-y-2 rounded-md border border-primary p-3">
            <p className="text-sm font-medium">請立刻複製簽章密鑰，離開後就看不到了</p>
            <code className="block break-all rounded bg-muted p-2 text-sm">{created.secret}</code>
            <div className="flex gap-2">
              <Button type="button" variant="outline" size="sm" onClick={() => copy(created.secret ?? "")}>
                {copied ? <Check className="mr-1 h-4 w-4" /> : <Copy className="mr-1 h-4 w-4" />}
                {copied ? "已複製" : "複製密鑰"}
              </Button>
              <Button type="button" variant="ghost" size="sm" onClick={() => setCreated(null)}>
                我已保存
              </Button>
            </div>
          </div>
        )}

        <form onSubmit={create} className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="hook-name">名稱</Label>
              <Input id="hook-name" value={name} onChange={(e) => setName(e.target.value)} maxLength={50} placeholder="例如：筆記網站" disabled={creating} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="hook-url">接收網址（https）</Label>
              <Input id="hook-url" value={url} onChange={(e) => setUrl(e.target.value)} maxLength={500} placeholder="https://example.com/rag-hook" inputMode="url" disabled={creating} aria-invalid={!httpsOnly} />
              {!httpsOnly && <p className="text-xs text-red-600">一律要使用 https://</p>}
            </div>
          </div>
          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">訂閱的事件</legend>
            {EVENTS.map((event) => (
              <label key={event.value} className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={events.includes(event.value)}
                  onCheckedChange={() => setEvents((prev) => (prev.includes(event.value) ? prev.filter((v) => v !== event.value) : [...prev, event.value]))}
                />
                {event.label}
                <code className="text-xs text-muted-foreground">{event.value}</code>
              </label>
            ))}
          </fieldset>
          <Button type="submit" disabled={!canCreate || creating}>
            {creating ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Plus className="mr-2 h-4 w-4" />}
            建立 Webhook
          </Button>
        </form>

        {loading ? (
          <div className="flex items-center gap-2 text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> 載入中...
          </div>
        ) : hooks.length === 0 ? (
          <p className="text-sm text-muted-foreground">還沒有任何 Webhook。</p>
        ) : (
          <ul className="divide-y rounded-md border">
            {hooks.map((hook) => (
              <li key={hook.id} className="p-3">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <button type="button" className="flex min-w-0 flex-1 items-start gap-1 text-left" onClick={() => toggleOpen(hook)} aria-expanded={openId === hook.id}>
                    {openId === hook.id ? <ChevronDown className="mt-1 h-4 w-4 shrink-0" /> : <ChevronRight className="mt-1 h-4 w-4 shrink-0" />}
                    <span className="min-w-0">
                      <span className="flex flex-wrap items-center gap-2 font-medium">
                        {hook.name}
                        <span className={`rounded-md px-1.5 py-0.5 text-xs font-medium ${hook.isActive ? "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300" : "bg-muted text-muted-foreground"}`}>
                          {hook.isActive ? "啟用中" : "已停用"}
                        </span>
                      </span>
                      <span className="block break-all text-xs text-muted-foreground">{hook.url}</span>
                      <span className="block text-xs text-muted-foreground">{(hook.events ?? []).join("、")}</span>
                      {!hook.isActive && hook.disabledReason && <span className="block text-xs text-amber-700">{hook.disabledReason}</span>}
                    </span>
                  </button>
                  <div className="flex shrink-0 gap-1">
                    <Button size="sm" variant="outline" onClick={() => test(hook)} disabled={workingId !== null || !hook.isActive}>
                      {workingId === `test-${hook.id}` ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Send className="mr-1 h-4 w-4" />}
                      測試
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => toggle(hook)} disabled={workingId !== null}>
                      {hook.isActive ? "停用" : "啟用"}
                    </Button>
                    <Button size="icon" variant="ghost" className="text-destructive" onClick={() => remove(hook)} disabled={workingId !== null} aria-label={`刪除 ${hook.name}`}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>

                {openId === hook.id && (
                  <div className="mt-3 space-y-2 border-t pt-3">
                    <p className="text-xs font-medium text-muted-foreground">最近的送出紀錄（失敗會自動重試，最多 6 次）</p>
                    {loadingDeliveries ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : deliveries.length === 0 ? (
                      <p className="text-xs text-muted-foreground">還沒有紀錄。</p>
                    ) : (
                      <ul className="space-y-2">
                        {deliveries.map((delivery) => {
                          const status = DELIVERY_STATUS[delivery.status ?? ""] ?? { label: delivery.status ?? "", className: "bg-muted" }
                          return (
                            <li key={delivery.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md bg-muted/40 p-2 text-xs">
                              <span className="min-w-0">
                                <span className={`mr-2 rounded-md px-1.5 py-0.5 font-medium ${status.className}`}>{status.label}</span>
                                <code>{delivery.event}</code> · 第 {delivery.attempts} 次 · {formatTime(delivery.createAt)}
                                {delivery.lastError && <span className="block break-words text-red-600">{delivery.lastError}</span>}
                                {delivery.status === "Pending" && delivery.nextAttemptAt && <span className="block text-muted-foreground">下次重試 {formatTime(delivery.nextAttemptAt)}</span>}
                              </span>
                              {delivery.status !== "Succeeded" && (
                                <Button size="sm" variant="outline" onClick={() => redeliver(hook, delivery)} disabled={workingId !== null || !hook.isActive}>
                                  {workingId === `redeliver-${delivery.id}` ? <Loader2 className="mr-1 h-3 w-3 animate-spin" /> : <RotateCw className="mr-1 h-3 w-3" />}
                                  重送
                                </Button>
                              )}
                            </li>
                          )
                        })}
                      </ul>
                    )}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}

        <details className="rounded-md border p-3 text-sm">
          <summary className="cursor-pointer font-medium">怎麼驗證收到的請求？</summary>
          <p className="mt-2 text-xs text-muted-foreground">
            每個請求都帶 <code>X-RAG-Signature</code>、<code>X-RAG-Timestamp</code>、<code>X-RAG-Event</code>、<code>X-RAG-Delivery</code>。內容只有編號與來源資訊，不含問答本文。
          </p>
          <pre className="mt-2 max-h-64 overflow-auto rounded bg-muted p-3 text-xs">{VERIFY_EXAMPLE}</pre>
        </details>
      </CardContent>
    </Card>
  )
}
