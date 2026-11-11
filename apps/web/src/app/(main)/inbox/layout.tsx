import type { Metadata } from "next"

export const metadata: Metadata = { title: "暫存區" }

// 暫存區是底部分頁之一，標題由頁面自己顯示（不需要返回鍵）
export default function Layout({ children }: { children: React.ReactNode }) {
  return children
}
