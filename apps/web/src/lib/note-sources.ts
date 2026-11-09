/**
 * learning-notes 同步進來的問答，答案最後一行是「來源：learning-notes entry#12」這種標記，
 * 用來對應回原本的筆記，對使用者沒有意義。顯示時把標記從內文拿掉，改成回答下方的來源標籤。
 *
 * 有設定 NEXT_PUBLIC_NOTES_URL（筆記網站網址）時，標籤會連到筆記網站的那一篇；沒設定就只顯示文字。
 */

export type NoteSource = { kind: "entry" | "concept" | "rule"; ref: string; label: string; href?: string }

const SOURCE_LINE = /^[ \t]*來源：learning-notes (entry|concept|rule)#([^\n]+?)[ \t]*$/gm

const KIND_LABEL: Record<NoteSource["kind"], string> = { entry: "改動紀錄", concept: "觀念", rule: "規則" }

const NOTES_URL = process.env.NEXT_PUBLIC_NOTES_URL?.trim().replace(/\/+$/, "") ?? ""

function labelOf(kind: NoteSource["kind"], ref: string): string {
  // 規則的 ref 是「專案／模組／標題」，只顯示標題；其他是數字 id
  return kind === "rule" ? `${KIND_LABEL.rule}：${ref.split("／").at(-1)}` : `${KIND_LABEL[kind]} #${ref}`
}

/** 改動紀錄和觀念卡用 ?id= 開單篇；規則沒有 id，開該專案的規則頁 */
function hrefOf(kind: NoteSource["kind"], ref: string): string | undefined {
  if (!NOTES_URL) return undefined
  if (kind === "rule") return `${NOTES_URL}/rules?${new URLSearchParams({ project: ref.split("／")[0] ?? "" })}`
  return `${NOTES_URL}/${kind === "entry" ? "entries" : "concepts"}?id=${encodeURIComponent(ref)}`
}

/** RAG Server 在公開知識的回答最前面加的提示（見 RAG_Server/main.py） */
const SHARED_NOTICE = "這段回答來自**公開知識**"

/** 問答答不出來時，把問題帶到筆記網站，由使用者在那裡確認才會記下來 */
export function gapHref(question: string): string | undefined {
  if (!NOTES_URL) return undefined
  return `${NOTES_URL}/gaps/new?${new URLSearchParams({ q: question.slice(0, 500) })}`
}

export const notesUrl = NOTES_URL

export function extractNoteSources(content: string): { body: string; sources: NoteSource[] } {
  const sources: NoteSource[] = []
  const seen = new Set<string>()
  // 公開知識是別人的筆記，單篇連結開不到，改連公開知識頁
  const shared = content.includes(SHARED_NOTICE)
  const body = content
    .replace(SOURCE_LINE, (_line, kind: NoteSource["kind"], ref: string) => {
      const key = `${kind}#${ref}`
      if (!seen.has(key)) {
        seen.add(key)
        sources.push({ kind, ref, label: labelOf(kind, ref), href: shared && NOTES_URL ? `${NOTES_URL}/explore` : hrefOf(kind, ref) })
      }
      return ""
    })
    .replace(/\n{3,}/g, "\n\n")
    .trim()
  return sources.length > 0 ? { body, sources } : { body: content, sources }
}
