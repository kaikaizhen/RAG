"use client"

import * as React from "react"

/** 註冊 Service Worker。只在正式環境啟用：開發模式的快取會讓改動看起來沒生效。 */
export function PwaRegister() {
  React.useEffect(() => {
    if (process.env.NODE_ENV !== "production" || !("serviceWorker" in navigator)) return
    navigator.serviceWorker.register("/sw.js", { scope: "/" }).catch((error) => {
      console.error("Service Worker 註冊失敗:", error)
    })
  }, [])
  return null
}
