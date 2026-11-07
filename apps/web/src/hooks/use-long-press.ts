import * as React from "react"

interface Options {
  /** 按住多久算長按（毫秒） */
  delay?: number
  /** 手指移動超過這個距離（px）就視為捲動，取消長按 */
  tolerance?: number
}

/**
 * 長按手勢（觸控 / 筆 / 滑鼠皆可）。長按觸發後會吃掉隨後的 click，避免放開手指時又開啟該項目；
 * 並擋掉長按時瀏覽器的內容選單（iOS 放大鏡 / Android 選單）。
 */
export function useLongPress(onLongPress: () => void, { delay = 500, tolerance = 10 }: Options = {}) {
  const timer = React.useRef<ReturnType<typeof setTimeout> | null>(null)
  const origin = React.useRef<{ x: number; y: number } | null>(null)
  const fired = React.useRef(false)
  const callback = React.useRef(onLongPress)
  callback.current = onLongPress

  const cancel = React.useCallback(() => {
    if (timer.current) clearTimeout(timer.current)
    timer.current = null
    origin.current = null
  }, [])

  React.useEffect(() => cancel, [cancel])

  return {
    onPointerDown: (e: React.PointerEvent) => {
      if (e.pointerType === "mouse" && e.button !== 0) return
      fired.current = false
      origin.current = { x: e.clientX, y: e.clientY }
      timer.current = setTimeout(() => {
        fired.current = true
        timer.current = null
        if (typeof navigator !== "undefined" && "vibrate" in navigator) navigator.vibrate(30)
        callback.current()
      }, delay)
    },
    onPointerMove: (e: React.PointerEvent) => {
      if (!origin.current) return
      if (Math.hypot(e.clientX - origin.current.x, e.clientY - origin.current.y) > tolerance) cancel()
    },
    onPointerUp: cancel,
    onPointerCancel: cancel,
    onPointerLeave: cancel,
    onContextMenu: (e: React.MouseEvent) => {
      // 觸控長按會連帶觸發 contextmenu；只在長按已觸發或正在計時時擋掉，桌面右鍵不受影響
      if (fired.current || timer.current) e.preventDefault()
    },
    onClickCapture: (e: React.MouseEvent) => {
      if (fired.current) {
        fired.current = false
        e.preventDefault()
        e.stopPropagation()
      }
    },
  }
}
