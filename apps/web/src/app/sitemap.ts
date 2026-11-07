import type { MetadataRoute } from "next"
import { absoluteUrl } from "@/lib/site"

/** 只列出公開、可收錄的頁面；App 內頁都需要登入，不放進 sitemap。沒有設定網站網址時不輸出。 */
export default function sitemap(): MetadataRoute.Sitemap {
  const about = absoluteUrl("/about")
  return about ? [{ url: about, changeFrequency: "monthly", priority: 1 }] : []
}
