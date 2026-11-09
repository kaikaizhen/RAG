"use client"

import * as React from "react"
import { Check, Copy } from "lucide-react"
import { extractNoteSources } from "@/lib/note-sources"

type MarkdownComponent = React.ComponentType<{ content: string }>

/**
 * 助理的回答，以 Markdown 渲染。Markdown 相關套件（含語法上色）不小，改成第一次需要時才載入，
 * 載入前先以純文字顯示，所以不會拖慢對話頁的首次載入，也不會出現空白。
 *
 * streaming：這則回答還在產生中。還沒收到任何文字時顯示「正在找答案」，產生完才出現複製按鈕。
 */
export function AssistantMessage({ content, streaming = false }: { content: string; streaming?: boolean }) {
  const [Markdown, setMarkdown] = React.useState<MarkdownComponent | null>(null)
  const { body, sources } = React.useMemo(() => extractNoteSources(content), [content])

  React.useEffect(() => {
    let alive = true
    import("@/components/markdown").then((module) => {
      if (alive) setMarkdown(() => module.default)
    })
    return () => {
      alive = false
    }
  }, [])

  if (!content && streaming) {
    return (
      <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground md:text-base">
        <span className="flex gap-1" aria-hidden="true">
          {[0, 150, 300].map((delay) => (
            <span
              key={delay}
              className="h-1.5 w-1.5 rounded-full bg-current motion-safe:animate-pulse"
              style={{ animationDelay: `${delay}ms` }}
            />
          ))}
        </span>
        正在知識庫裡找答案
      </p>
    )
  }

  return (
    <>
      {Markdown ? <Markdown content={body} /> : <p className="whitespace-pre-wrap text-sm md:text-base">{body}</p>}
      {(sources.length > 0 || !streaming) && (
        <div className="mt-3 flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
          {sources.length > 0 && <span>來源</span>}
          {sources.map((source) => {
            const key = `${source.kind}#${source.ref}`
            // 手機上要好按：至少 32px 高
            const className = "inline-flex min-h-8 items-center rounded-full border bg-background/60 px-2.5 md:min-h-6"
            return source.href ? (
              <a
                key={key}
                href={source.href}
                target="_blank"
                rel="noopener noreferrer"
                title={`在筆記網站查看 learning-notes ${key}`}
                className={`${className} transition-colors hover:bg-background hover:text-foreground`}
              >
                {source.label}
              </a>
            ) : (
              <span key={key} title={`learning-notes ${key}`} className={className}>
                {source.label}
              </span>
            )
          })}
          {!streaming && body && <CopyButton text={body} />}
        </div>
      )}
    </>
  )
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = React.useState(false)

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* 剪貼簿被瀏覽器封鎖時不做任何事 */
    }
  }

  return (
    <button
      type="button"
      onClick={copy}
      className="ml-auto inline-flex min-h-8 items-center gap-1 rounded-full px-2.5 transition-colors hover:bg-background hover:text-foreground md:min-h-6"
      aria-label={copied ? "已複製回答" : "複製回答"}
    >
      {copied ? <Check className="h-3.5 w-3.5" aria-hidden="true" /> : <Copy className="h-3.5 w-3.5" aria-hidden="true" />}
      {copied ? "已複製" : "複製"}
    </button>
  )
}
