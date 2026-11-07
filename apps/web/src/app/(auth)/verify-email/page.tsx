'use client'

import * as React from "react"
import Link from "next/link"
import { useSearchParams } from "next/navigation"
import { CheckCircle2, Loader2, XCircle } from "lucide-react"
import { Button } from "@/components/ui/button"
import { CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { describeError, getClient } from "@/lib/rag"
import { Logo } from "@/components/logo"

type State = { kind: "verifying" } | { kind: "done" } | { kind: "failed"; message: string }

function VerifyEmail() {
  const token = useSearchParams().get("token") ?? ""
  const [state, setState] = React.useState<State>(token ? { kind: "verifying" } : { kind: "failed", message: "缺少驗證碼，請使用信件中的完整連結" })
  // React 開發模式會重複執行 effect；同一個連結只送出一次
  const sent = React.useRef(false)

  React.useEffect(() => {
    if (!token || sent.current) return
    sent.current = true
    getClient()
      .auth.verifyEmail(token)
      .then(() => setState({ kind: "done" }))
      .catch((error) => setState({ kind: "failed", message: describeError(error, "驗證失敗，請稍後再試") }))
  }, [token])

  return (
    <div className="flex min-h-dvh items-center justify-center p-4">
      <div className="flex w-full max-w-md flex-col gap-6 py-6">
        <CardHeader>
          <div className="mb-2 flex justify-center">
            <Logo size={80} priority animated />
          </div>
          <CardTitle className="text-2xl">驗證信箱</CardTitle>
          <CardDescription>完成驗證後即可登入</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {state.kind === "verifying" && (
            <div className="flex items-center gap-2 text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              <span>驗證中...</span>
            </div>
          )}
          {state.kind === "done" && (
            <>
              <div className="flex items-start gap-2 rounded border border-green-200 bg-green-50 p-3 text-sm text-green-700">
                <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" />
                <span>信箱已驗證，帳號已啟用，並已為您建立專屬的個人空間。</span>
              </div>
              <Button asChild className="w-full">
                <Link href="/login">前往登入</Link>
              </Button>
            </>
          )}
          {state.kind === "failed" && (
            <>
              <div className="flex items-start gap-2 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-600">
                <XCircle className="mt-0.5 h-5 w-5 shrink-0" />
                <span>{state.message}</span>
              </div>
              <Button asChild variant="outline" className="w-full">
                <Link href="/signup">重新註冊</Link>
              </Button>
            </>
          )}
        </CardContent>
      </div>
    </div>
  )
}

export default function VerifyEmailPage() {
  // useSearchParams 需要 Suspense 邊界才能靜態預先產生頁面
  return (
    <React.Suspense fallback={null}>
      <VerifyEmail />
    </React.Suspense>
  )
}
