"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { getClient, getProfile, type Profile } from "@/lib/rag"

/**
 * 取得目前登入者；未登入或 session 過期時導向 /login。
 * ready=false 代表還在讀取（避免 SSR 與瀏覽器端內容不一致）。
 */
export function useProfile(options: { redirect?: boolean } = {}) {
  const { redirect = true } = options
  const router = useRouter()
  const [profile, setProfile] = React.useState<Profile | null>(null)
  const [ready, setReady] = React.useState(false)

  React.useEffect(() => {
    const sync = () => setProfile(getProfile())
    sync()
    setReady(true)
    window.addEventListener("profileChanged", sync)

    const rag = getClient()
    const off = rag.on("session-expired", () => {
      if (redirect) router.replace("/login")
    })
    rag.auth.session().then((session) => {
      if (!session && redirect) router.replace("/login")
    })
    return () => {
      window.removeEventListener("profileChanged", sync)
      off()
    }
  }, [redirect, router])

  return { profile, ready }
}
