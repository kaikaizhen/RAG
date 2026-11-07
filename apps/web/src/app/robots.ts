import type { MetadataRoute } from "next"
import { siteUrl } from "@/lib/site"

/** 私有的 App 頁面與 API：任何爬蟲都不應收錄（頁面本身另有 noindex 與登入保護） */
const PRIVATE_PATHS = ["/admin-settings", "/profile", "/verify-email", "/api/"]

/**
 * AI 搜尋型爬蟲：用於即時搜尋與引用，允許讀取公開頁面（RAGence 要被 AI 搜尋引用）。
 * AI 訓練型爬蟲：只用於模型訓練、不帶來引用流量，整站禁止。
 */
const AI_SEARCH_BOTS = [
  "GPTBot",
  "OAI-SearchBot",
  "ChatGPT-User",
  "ClaudeBot",
  "PerplexityBot",
  "Google-Extended",
  "GoogleOther",
  "Applebot-Extended",
]
const AI_TRAINING_BOTS = ["CCBot", "anthropic-ai", "Bytespider", "cohere-ai"]

export default function robots(): MetadataRoute.Robots {
  const base = siteUrl()
  return {
    rules: [
      { userAgent: "*", allow: ["/about", "/llms.txt"], disallow: PRIVATE_PATHS },
      { userAgent: AI_SEARCH_BOTS, allow: ["/about", "/llms.txt"], disallow: PRIVATE_PATHS },
      { userAgent: AI_TRAINING_BOTS, disallow: "/" },
    ],
    ...(base && { sitemap: new URL("/sitemap.xml", base).toString(), host: base.origin }),
  }
}
