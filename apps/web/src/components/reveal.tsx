"use client"

import * as React from "react"
import { cn } from "@/lib/utils"

/**
 * 捲動到畫面內時淡入上移。伺服器端與未執行 JS 時內容預設可見（不影響閱讀與爬蟲）；
 * 只有掛載後仍在畫面下方的區塊才會先隱藏再淡入。尊重「減少動態效果」設定。
 */
export function Reveal({ children, className, delay = 0 }: { children: React.ReactNode; className?: string; delay?: number }) {
  const ref = React.useRef<HTMLDivElement>(null)
  const [state, setState] = React.useState<"visible" | "hidden">("visible")

  React.useEffect(() => {
    const el = ref.current
    if (!el || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return
    if (el.getBoundingClientRect().top < window.innerHeight * 0.9) return // 一開始就在畫面內
    setState("hidden")
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setState("visible")
          observer.disconnect()
        }
      },
      { threshold: 0.15 },
    )
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  return (
    <div
      ref={ref}
      style={{ transitionDelay: `${delay}ms` }}
      className={cn("transition-all duration-700 ease-out", state === "hidden" ? "translate-y-8 opacity-0" : "translate-y-0 opacity-100", className)}
    >
      {children}
    </div>
  )
}
