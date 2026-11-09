/**
 * learning-notes 同步進來的問答，答案最後一行是「來源：learning-notes entry#12」這種標記，
 * 用來對應回原本的筆記，對使用者沒有意義。顯示時把標記從內文拿掉，改成回答下方的來源標籤。
 *
 * 有設定 NEXT_PUBLIC_NOTES_URL（筆記網站網址）時，標籤會連到筆記網站搜尋那篇筆記；沒設定就只顯示文字。
 */

export type NoteSource = { kind: "entry" | "concept" | "rule"; ref: string; label: string; href?: string }

const SOURCE_LINE = /^[ \t]*來源：learning-notes (entry|concept|rule)#([^\n]+?)[ \t]*$/gm
// 同步時每篇答案都以「## 標題」開頭，用來找標記屬於哪一篇
const HEADING_LINE = /^## (.+)$/gm

const KIND_LABEL: Record<NoteSource["kind"], string> = { entry: "改動紀錄", concept: "觀念", rule: "規則" }

const NOTES_URL = process.env.NEXT_PUBLIC_NOTES_URL?.trim().replace(/\/+$/, "") ?? ""

function labelOf(kind: NoteSource["kind"], ref: string): string {
  // 規則的 ref 是「專案／模組／標題」，只顯示標題；其他是數字 id
  return kind === "rule" ? `${KIND_LABEL.rule}：${ref.split("／").at(-1)}` : `${KIND_LABEL[kind]} #${ref}`
}

/** 筆記網站沒有單篇頁面，所以連到列表頁並帶上搜尋條件 */
function hrefOf(kind: NoteSource["kind"], ref: string, title: string | undefined): string | undefined {
  if (!NOTES_URL) return undefined
  if (kind === "rule") return `${NOTES_URL}/rules?${new URLSearchParams({ project: ref.split("／")[0] ?? "" })}`
  const path = kind === "entry" ? "entries" : "concepts"
  return title ? `${NOTES_URL}/${path}?${new URLSearchParams({ query: title })}` : `${NOTES_URL}/${path}`
}

export function extractNoteSources(content: string): { body: string; sources: NoteSource[] } {
  const sources: NoteSource[] = []
  const seen = new Set<string>()
  const headings = [...content.matchAll(HEADING_LINE)].map((m) => ({ index: m.index, title: m[1]!.trim() }))

  const body = content
    .replace(SOURCE_LINE, (_line, kind: NoteSource["kind"], ref: string, offset: number) => {
      const key = `${kind}#${ref}`
      if (!seen.has(key)) {
        seen.add(key)
        // LLM 整理過的回答只列出來源，前面不一定有對應的標題，這時就不帶搜尋條件
        const title = kind === "rule" ? undefined : headings.filter((h) => h.index < offset).at(-1)?.title
        sources.push({ kind, ref, label: labelOf(kind, ref), href: hrefOf(kind, ref, title) })
      }
      return ""
    })
    .replace(/\n{3,}/g, "\n\n")
    .trim()
  return sources.length > 0 ? { body, sources } : { body: content, sources }
}
