"use client"

import * as React from "react"
import { extractNoteSources } from "@/lib/note-sources"

type MarkdownComponent = React.ComponentType<{ content: string }>

/**
 * 助理的回答，以 Markdown 渲染。Markdown 相關套件（含語法上色）不小，改成第一次需要時才載入，
 * 載入前先以純文字顯示，所以不會拖慢對話頁的首次載入，也不會出現空白。
 */
export function AssistantMessage({ content }: { content: string }) {
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

  return (
    <>
      {Markdown ? <Markdown content={body} /> : <p className="whitespace-pre-wrap text-sm md:text-base">{body}</p>}
      {sources.length > 0 && (
        <div className="mt-3 flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
          <span>來源</span>
          {sources.map((source) => (
            <span
              key={`${source.kind}#${source.ref}`}
              title={`learning-notes ${source.kind}#${source.ref}`}
              className="rounded-full border bg-background/60 px-2 py-0.5"
            >
              {source.label}
            </span>
          ))}
        </div>
      )}
    </>
  )
}
