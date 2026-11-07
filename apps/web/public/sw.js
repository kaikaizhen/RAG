/*
 * RAGence Service Worker：只快取「靜態外殼」，讓網站可安裝、離線時顯示提示頁。
 *
 * 刻意不快取的東西（這是問答系統，快取會讓使用者看到舊答案或別人的資料）：
 *   - 任何跨來源請求（.NET API 通常在另一個網域）
 *   - /api/ 路徑、非 GET 請求、串流（text/event-stream）
 *   - 頁面 HTML（一律走網路，失敗才顯示 /offline.html）
 * 只快取：build 產生的 /_next/static/（檔名含雜湊，內容不會變）與圖示。
 * 改動這支檔案時請調高 VERSION，舊快取會在 activate 時清掉。
 */
const VERSION = "v1"
const STATIC_CACHE = `ragence-static-${VERSION}`
const OFFLINE_URL = "/offline.html"
const PRECACHE = [OFFLINE_URL, "/icons/icon-192.png", "/icons/icon-512.png"]

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(STATIC_CACHE)
      .then((cache) => cache.addAll(PRECACHE))
      .then(() => self.skipWaiting()),
  )
})

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key.startsWith("ragence-") && key !== STATIC_CACHE).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  )
})

function isCacheableStatic(url) {
  return url.pathname.startsWith("/_next/static/") || url.pathname.startsWith("/icons/")
}

self.addEventListener("fetch", (event) => {
  const request = event.request
  if (request.method !== "GET") return

  const url = new URL(request.url)
  if (url.origin !== self.location.origin) return // API 與其他來源：完全不介入
  if (url.pathname.startsWith("/api/")) return
  if ((request.headers.get("accept") || "").includes("text/event-stream")) return

  // 頁面導覽：永遠走網路；離線時才顯示提示頁
  if (request.mode === "navigate") {
    event.respondWith(fetch(request).catch(() => caches.match(OFFLINE_URL)))
    return
  }

  // 靜態資源：快取優先，沒有才下載並存起來
  if (isCacheableStatic(url)) {
    event.respondWith(
      caches.match(request).then(
        (cached) =>
          cached ||
          fetch(request).then((response) => {
            if (response.ok) {
              const copy = response.clone()
              caches.open(STATIC_CACHE).then((cache) => cache.put(request, copy))
            }
            return response
          }),
      ),
    )
  }
})
