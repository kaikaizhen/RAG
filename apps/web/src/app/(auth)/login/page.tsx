'use client'

import { Button } from "@/components/ui/button"
import {
  CardContent,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import Link from "next/link"
import { useState, FormEvent } from "react"
import { useRouter } from "next/navigation"
import { describeError, signIn } from "@/lib/rag"
import { Logo } from "@/components/logo"

export default function LoginPage() {
  const router = useRouter()
  const [account, setAccount] = useState('')
  const [password, setPassword] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setIsLoading(true)
    setError('')

    try {
      await signIn(account, password)
      router.push('/')
    } catch (err) {
      setError(describeError(err, '登入失敗，請稍後再試'))
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="flex min-h-dvh items-center justify-center p-4">
      <div className="flex w-full max-w-sm flex-col gap-6 py-6">
        <CardHeader className="justify-items-center gap-3 text-center">
          <Logo size={96} priority animated />
          <div>
            <CardTitle className="text-2xl">登入 RAGence</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">以知識庫為依據的 AI 問答平台</p>
          </div>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit}>
            <div className="flex flex-col gap-6">
              {error && (
                <div className="text-sm text-red-500 bg-red-50 p-3 rounded border border-red-200">
                  <p>{error}</p>
                  {error.includes('帳號或密碼') && (
                    <Link href="/signup" className="inline-block mt-2 text-primary hover:underline font-medium">
                      還沒有帳號？前往註冊 →
                    </Link>
                  )}
                </div>
              )}
              <div className="grid gap-2">
                <Label htmlFor="account">帳號（電子信箱）</Label>
                <Input
                  id="account"
                  type="text"
                  placeholder="請輸入帳號"
                  value={account}
                  onChange={(e) => setAccount(e.target.value)}
                  required
                  disabled={isLoading}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="password">密碼</Label>
                <Input 
                  id="password" 
                  type="password" 
                  placeholder="請輸入密碼"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  disabled={isLoading}
                />
              </div>
            </div>
            <CardFooter className="flex-col gap-2 px-0 pt-6">
              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? '登入中...' : '登入'}
              </Button>
              <p className="text-center text-sm text-muted-foreground">
                還沒有帳號？
                <Link href="/signup" className="ml-1 font-medium text-foreground underline-offset-4 hover:underline">
                  註冊
                </Link>
              </p>
            </CardFooter>
          </form>
        </CardContent>
      </div>
    </div>
  )
}
