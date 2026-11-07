"use client"

import { Loader2 } from "lucide-react"
import { useProfile } from "@/lib/use-profile"

/** 需要登入的區域：尚未確認登入狀態時不渲染內容，未登入由 useProfile 導向 /login */
export function AuthGate({ children }: { children: React.ReactNode }) {
  const { profile, ready } = useProfile()
  if (!ready || !profile) {
    return (
      <div className="flex h-screen w-full items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    )
  }
  return <>{children}</>
}
