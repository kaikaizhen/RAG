import type { Metadata } from "next"
import { MobileTopBar } from "@/components/mobile-top-bar"

export const metadata: Metadata = { title: "知識庫管理" }

// dvh：手機瀏覽器 / PWA 的網址列收合時，100vh 會比實際可視高度高，內容會被切掉
export default function Layout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex h-dvh flex-col">
      <MobileTopBar title="知識庫管理" />
      <div className="min-h-0 flex-1">{children}</div>
    </div>
  )
}
