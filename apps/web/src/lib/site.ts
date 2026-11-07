export const SITE_NAME = "RAGence"
export const SITE_TAGLINE = "以知識庫為依據的 AI 問答平台"
export const SITE_DESCRIPTION =
  "RAGence 讓團隊把常見問題整理成知識庫，之後問一句就能得到有依據的答案；找不到可靠的答案時會直接說找不到，不會亂猜。"

/**
 * 網站的正式網址（GEO / SEO 的 canonical、sitemap、結構化資料都需要絕對網址）。
 * 由 NEXT_PUBLIC_SITE_URL 設定；未設定時回傳 null，呼叫端就不輸出需要絕對網址的內容。
 */
export function siteUrl(): URL | null {
  const raw = process.env.NEXT_PUBLIC_SITE_URL?.trim()
  if (!raw) return null
  try {
    return new URL(raw)
  } catch {
    return null
  }
}

export function absoluteUrl(path: string): string | null {
  const base = siteUrl()
  return base ? new URL(path, base).toString() : null
}
