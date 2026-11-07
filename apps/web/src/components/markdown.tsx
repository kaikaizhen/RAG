"use client"

import * as React from "react"
import ReactMarkdown, { type Components } from "react-markdown"
import rehypeHighlight from "rehype-highlight"
import remarkBreaks from "remark-breaks"
import remarkGfm from "remark-gfm"
import { Check, Copy } from "lucide-react"
import { cn } from "@/lib/utils"

/**
 * Markdown 渲染（像 ChatGPT）：GFM 表格 / 清單 / 刪除線 / 待辦清單、程式碼區塊（語法上色 + 複製）、連結。
 *
 * 安全：react-markdown 預設不渲染原始 HTML（<script>、<img onerror> 都會被當成文字），
 * 連結的 javascript: 等危險協定也會被濾掉；外部連結一律 noopener noreferrer。
 * remark-breaks：單一換行也會換行（知識庫的答案多半是照行書寫，不想因為 Markdown 規則被合併成一行）。
 */

function textOf(node: React.ReactNode): string {
  if (node == null || typeof node === "boolean") return ""
  if (typeof node === "string" || typeof node === "number") return String(node)
  if (Array.isArray(node)) return node.map(textOf).join("")
  if (React.isValidElement<{ children?: React.ReactNode }>(node)) return textOf(node.props.children)
  return ""
}

function CodeBlock({ children }: { children?: React.ReactNode }) {
  const [copied, setCopied] = React.useState(false)
  const code = React.Children.toArray(children)[0]
  const className = React.isValidElement<{ className?: string }>(code) ? code.props.className : undefined
  const language = /language-([\w+-]+)/.exec(className ?? "")?.[1]
  const raw = textOf(children).replace(/\n$/, "")

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(raw)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* 剪貼簿被瀏覽器封鎖時不做任何事 */
    }
  }

  return (
    <div className="not-prose my-3 overflow-hidden rounded-xl border bg-background text-foreground">
      <div className="flex items-center justify-between border-b bg-muted/60 px-3 py-1.5 text-xs text-muted-foreground">
        <span>{language ?? "程式碼"}</span>
        <button
          type="button"
          onClick={copy}
          className="flex items-center gap-1 rounded px-1.5 py-0.5 transition-colors hover:bg-background hover:text-foreground"
          aria-label="複製程式碼"
        >
          {copied ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
          {copied ? "已複製" : "複製"}
        </button>
      </div>
      <pre className="overflow-x-auto p-3 text-[13px] leading-relaxed">{children}</pre>
    </div>
  )
}

const components: Components = {
  pre: ({ children }) => <CodeBlock>{children}</CodeBlock>,
  a: ({ children, href }) => (
    <a href={href} target="_blank" rel="noopener noreferrer">
      {children}
    </a>
  ),
  table: ({ children }) => (
    <div className="not-prose my-3 overflow-x-auto rounded-lg border">
      <table className="w-full border-collapse text-sm">{children}</table>
    </div>
  ),
  th: ({ children }) => <th className="border-b bg-muted/60 px-3 py-2 text-left font-semibold">{children}</th>,
  td: ({ children }) => <td className="border-b px-3 py-2 align-top last:border-b-0">{children}</td>,
  // 不載入答案裡指定的外部圖片（避免被當成追蹤點、或把大圖塞進對話）：改顯示成連結
  img: ({ src, alt }) =>
    typeof src === "string" ? (
      <a href={src} target="_blank" rel="noopener noreferrer">
        {alt || src}
      </a>
    ) : null,
}

const remarkPlugins = [remarkGfm, remarkBreaks]
const rehypePlugins = [[rehypeHighlight, { detect: false, ignoreMissing: true }]] as React.ComponentProps<typeof ReactMarkdown>["rehypePlugins"]

function MarkdownImpl({ content, className }: { content: string; className?: string }) {
  return (
    <div
      className={cn(
        "prose prose-sm max-w-none break-words md:prose-base dark:prose-invert",
        "prose-p:my-2 prose-headings:mb-2 prose-headings:mt-4 prose-li:my-0.5 prose-ul:my-2 prose-ol:my-2",
        "prose-a:text-primary prose-a:underline-offset-4",
        "prose-blockquote:border-l-2 prose-blockquote:font-normal prose-blockquote:not-italic prose-blockquote:before:content-none prose-blockquote:after:content-none [&_blockquote_p::after]:content-none [&_blockquote_p::before]:content-none",
        // GFM 待辦清單：不要項目符號（checkbox 本身就是標記）
        "[&_.contains-task-list]:list-none [&_.contains-task-list]:pl-0 [&_.task-list-item]:pl-0 [&_.task-list-item::marker]:content-none [&_.task-list-item_input]:mr-2",
        "prose-code:rounded prose-code:bg-background/70 prose-code:px-1 prose-code:py-0.5 prose-code:font-medium prose-code:before:content-none prose-code:after:content-none",
        "[&>*:first-child]:mt-0 [&>*:last-child]:mb-0",
        className,
      )}
    >
      <ReactMarkdown remarkPlugins={remarkPlugins} rehypePlugins={rehypePlugins} components={components}>
        {content}
      </ReactMarkdown>
    </div>
  )
}

/** 串流時每收到一段文字就會重新渲染；內容沒變時略過 */
export default React.memo(MarkdownImpl)
