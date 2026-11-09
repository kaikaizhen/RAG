/**
 * learning-notes 同步進來的問答，答案最後一行是「來源：learning-notes entry#12」這種標記，
 * 用來對應回原本的筆記，對使用者沒有意義。顯示時把標記從內文拿掉，改成回答下方的來源標籤。
 */

export type NoteSource = { kind: "entry" | "concept" | "rule"; ref: string; label: string }

const SOURCE_LINE = /^[ \t]*來源：learning-notes (entry|concept|rule)#([^\n]+?)[ \t]*$/gm

const KIND_LABEL: Record<NoteSource["kind"], string> = { entry: "改動紀錄", concept: "觀念", rule: "規則" }

function labelOf(kind: NoteSource["kind"], ref: string): string {
  // 規則的 ref 是「專案／模組／標題」，只顯示標題；其他是數字 id
  return kind === "rule" ? `${KIND_LABEL.rule}：${ref.split("／").at(-1)}` : `${KIND_LABEL[kind]} #${ref}`
}

export function extractNoteSources(content: string): { body: string; sources: NoteSource[] } {
  const sources: NoteSource[] = []
  const seen = new Set<string>()
  const body = content
    .replace(SOURCE_LINE, (_line, kind: NoteSource["kind"], ref: string) => {
      const key = `${kind}#${ref}`
      if (!seen.has(key)) {
        seen.add(key)
        sources.push({ kind, ref, label: labelOf(kind, ref) })
      }
      return ""
    })
    .replace(/\n{3,}/g, "\n\n")
    .trim()
  return sources.length > 0 ? { body, sources } : { body: content, sources }
}
