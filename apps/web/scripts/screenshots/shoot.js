/*
 * 重新產生官網（/about）使用的產品畫面。資料都是 mock-api.js 的範例資料，不連任何真實系統。
 *
 * 1. node scripts/screenshots/mock-api.js                      # 假後端，port 4010
 * 2. NEXT_PUBLIC_RAG_API_URL=http://localhost:4010 npm run build && npx next start -p 3000
 *    （請用正式建置：開發模式會有浮動的除錯元件）
 * 3. npm i --no-save puppeteer-core && node scripts/screenshots/shoot.js
 *
 * 需要本機安裝 Google Chrome（路徑見下方 CHROME）。輸出到 public/screenshots/。
 */
const puppeteer = require("puppeteer-core")
const out = process.argv[2] || require("path").join(__dirname, "../../public/screenshots")
const CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

async function login(page) {
  await page.goto("http://localhost:3000/login", { waitUntil: "networkidle0" })
  await page.type("#account", "xiaoming@example.com")
  await page.type("#password", "demo-password")
  await Promise.all([page.waitForNavigation({ waitUntil: "networkidle0" }), page.click("button[type=submit]")])
}
const clickText = (page, text) =>
  page.evaluate((t) => {
    const el = [...document.querySelectorAll("button, a, [role=button]")].find((e) => e.textContent.includes(t))
    if (el) el.click()
    return !!el
  }, text)
const shot = (page, name) => page.screenshot({ path: `${out}/${name}.jpg`, type: "jpeg", quality: 86 })

;(async () => {
  const browser = await puppeteer.launch({ executablePath: CHROME, headless: "new", args: ["--hide-scrollbars"] })
  try {
    // ---- 桌面：淺色
    const page = await browser.newPage()
    await page.setViewport({ width: 1440, height: 900, deviceScaleFactor: 1 })
    await login(page)
    await sleep(800)
    await shot(page, "chat-home")                       // 空畫面 + 問候語 + 側邊欄
    await clickText(page, "出差報帳需要準備哪些文件")
    await sleep(800)
    await page.type("textarea", "在家要怎麼連上公司 VPN？")
    await page.keyboard.press("Enter")
    await sleep(3500)                                    // 等串流結束
    await shot(page, "chat-conversation")
    await clickText(page, "新對話")
    await sleep(500)
    await page.type("textarea", "公司的股票代號是什麼？")
    await page.keyboard.press("Enter")
    await sleep(1800)
    await shot(page, "chat-notfound")                    // 找不到可靠內容時誠實回覆
    await page.goto("http://localhost:3000/admin-settings", { waitUntil: "networkidle0" })
    await sleep(800)
    await shot(page, "knowledge-manage")
    await page.goto("http://localhost:3000/admin-settings/history", { waitUntil: "networkidle0" })
    await sleep(800)
    await shot(page, "knowledge-records")

    // ---- 手機：淺色
    const light = await browser.newPage()
    await light.setViewport({ width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true })
    await login(light)
    await sleep(800)
    await light.type("textarea", "在家要怎麼連上公司 VPN？")
    await light.keyboard.press("Enter")
    await sleep(3500)
    await shot(light, "mobile-light")

    // ---- 手機：深色
    const mobile = await browser.newPage()
    await mobile.emulateMediaFeatures([{ name: "prefers-color-scheme", value: "dark" }])
    await mobile.evaluateOnNewDocument(() => { try { localStorage.setItem("theme", "dark") } catch {} })
    await mobile.setViewport({ width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true })
    await login(mobile)
    await sleep(800)
    await mobile.type("textarea", "在家要怎麼連上公司 VPN？")
    await mobile.keyboard.press("Enter")
    await sleep(3500)
    await shot(mobile, "mobile-dark")
  } finally {
    await browser.close()
  }
})().catch((e) => { console.error(e); process.exit(1) })
