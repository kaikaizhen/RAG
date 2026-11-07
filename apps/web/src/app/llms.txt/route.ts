import { FAQ } from "@/app/about/content"
import { SITE_DESCRIPTION, SITE_NAME, absoluteUrl } from "@/lib/site"

export const dynamic = "force-static"

/** llms.txt：給 AI 系統的站台摘要（格式見 llmstxt.org）。連結一律用絕對網址，因此需要設定 NEXT_PUBLIC_SITE_URL。 */
export function GET() {
  const link = (path: string) => absoluteUrl(path) ?? path
  const body = [
    `# ${SITE_NAME}`,
    "",
    `> ${SITE_DESCRIPTION}`,
    "",
    "## 主要頁面",
    "",
    `- [產品介紹與常見問題](${link("/about")}): 說明 ${SITE_NAME} 是什麼、如何以知識庫回答問題、知識狀態與資料隔離方式，以及常見問題。`,
    "",
    "## 重點事實",
    "",
    "- 類型：以知識庫為依據的問答平台，把團隊整理好的問答變成隨問隨答的小幫手",
    "- 回答方式：答案只來自使用者整理的內容；找不到可靠答案時會明確告知，不會亂猜",
    "- 知識管理：每筆內容可標示已驗證、可能過期、推論、已取代；已取代的內容不再用於回答",
    "- 隱私：註冊後有專屬的個人空間，問答與對話不會被其他使用者看到",
    "- 使用方式：手機與電腦瀏覽器皆可使用，也能加到主畫面；介面為繁體中文",
    "",
    "## 常見問題",
    "",
    ...FAQ.map((item) => `- ${item.question}`),
    "",
    "## 說明",
    "",
    "- 登入後的對話、知識庫管理與個人資料頁面屬於私人內容，不提供爬取。",
    "",
  ].join("\n")
  return new Response(body, { headers: { "Content-Type": "text/plain; charset=utf-8", "Cache-Control": "public, max-age=3600" } })
}
