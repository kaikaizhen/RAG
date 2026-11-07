'use client'

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import Link from "next/link"
import { useState, FormEvent } from "react"
import { useRouter } from "next/navigation"
import { RagServiceUnavailableError } from "@rag/client-sdk"
import { describeError, getClient } from "@/lib/rag"
import { Logo } from "@/components/logo"

// 判斷字串是否包含中文字元
const containsChinese = (text: string): boolean => {
  return /[\u4e00-\u9fa5]/.test(text)
}

// 判斷字串是否為英文（只包含英文字母和空格）
const isEnglish = (text: string): boolean => {
  return /^[a-zA-Z\s]+$/.test(text)
}

// 智能判斷名稱語言類型
const detectNameLanguage = (name: string): 'zh' | 'en' | 'other' => {
  if (containsChinese(name)) {
    return 'zh'
  } else if (isEnglish(name)) {
    return 'en'
  } else {
    return 'other'
  }
}

export default function SignUpPage() {
  const router = useRouter()
  const [account, setAccount] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [name, setName] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [detectedLanguage, setDetectedLanguage] = useState<string>('')
  const [submittedEmail, setSubmittedEmail] = useState('')

  // 當名稱改變時，即時顯示偵測到的語言
  const handleNameChange = (value: string) => {
    setName(value)
    if (value.trim()) {
      const lang = detectNameLanguage(value)
      if (lang === 'zh') {
        setDetectedLanguage('偵測到：中文')
      } else if (lang === 'en') {
        setDetectedLanguage('偵測到：英文')
      } else {
        setDetectedLanguage('偵測到：其他語言')
      }
    } else {
      setDetectedLanguage('')
    }
  }

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setIsLoading(true)
    setError('')
    setSuccess('')

    // 驗證密碼是否一致
    if (password !== confirmPassword) {
      setError('密碼與確認密碼不一致')
      setIsLoading(false)
      return
    }

    // 驗證密碼長度
    if (password.length < 8) {
      setError('密碼長度至少需要 8 個字元')
      setIsLoading(false)
      return
    }

    // 驗證名稱
    if (!name.trim()) {
      setError('請輸入您的名稱')
      setIsLoading(false)
      return
    }

    // 判斷名稱語言並組成 API 資料
    const languageType = detectNameLanguage(name)
    const requestData = {
      account,
      password,
      nameZh: languageType === 'zh' ? name : '',
      nameEn: languageType === 'en' ? name : '',
      nameOther: languageType === 'other' ? name : '',
    }

    try {
      // 帳號 = 信箱；伺服器寄出驗證信，點擊信中連結後才會建立帳號。
      // 不論信箱是否已註冊，回應都相同，所以這裡只能提示「若可註冊，驗證信已寄出」
      const result = await getClient().auth.register(requestData)
      setSubmittedEmail(account.trim())
      setSuccess(result.message ?? '驗證信已寄出，請到信箱點擊連結完成註冊')
    } catch (err) {
      setError(
        err instanceof RagServiceUnavailableError
          ? '目前未開放註冊，或驗證信暫時無法寄出，請稍後再試'
          : describeError(err, '註冊時發生錯誤'),
      )
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <div className="mb-2 flex justify-center">
            <Logo size={80} priority animated />
          </div>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="text-2xl">建立新帳號</CardTitle>
              <CardDescription className="mt-2">
                填寫以下資訊來建立您的 RAGence 帳號
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit}>
            <div className="flex flex-col gap-4">
              {error && (
                <div className="text-sm text-red-500 bg-red-50 p-3 rounded border border-red-200">
                  {error}
                </div>
              )}
              {success && (
                <div className="text-sm text-green-600 bg-green-50 p-3 rounded border border-green-200 space-y-1">
                  <p>{success}</p>
                  {submittedEmail && (
                    <p className="text-xs text-green-700">
                      請檢查 {submittedEmail} 的收件匣（也看看垃圾郵件）。連結 24 小時內有效；
                      若沒收到，稍等一分鐘後可用同一個信箱重新送出。
                    </p>
                  )}
                </div>
              )}
              
              <div className="grid gap-2">
                <Label htmlFor="name">姓名 *</Label>
                <Input
                  id="name"
                  type="text"
                  placeholder="請輸入您的姓名（支援中文、英文或其他語言）"
                  value={name}
                  onChange={(e) => handleNameChange(e.target.value)}
                  required
                  disabled={isLoading}
                />
                {detectedLanguage && (
                  <p className="text-xs text-muted-foreground">
                    {detectedLanguage}
                  </p>
                )}
              </div>

              <div className="grid gap-2">
                <Label htmlFor="account">電子信箱 *（同時是登入帳號）</Label>
                <Input
                  id="account"
                  type="email"
                  autoComplete="email"
                  placeholder="請輸入電子信箱"
                  value={account}
                  onChange={(e) => setAccount(e.target.value)}
                  required
                  disabled={isLoading}
                />
              </div>

              <div className="grid gap-2">
                <Label htmlFor="password">密碼 *</Label>
                <Input 
                  id="password" 
                  type="password" 
                  placeholder="至少 8 個字元"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  disabled={isLoading}
                  minLength={8}
                />
              </div>

              <div className="grid gap-2">
                <Label htmlFor="confirmPassword">確認密碼 *</Label>
                <Input 
                  id="confirmPassword" 
                  type="password" 
                  placeholder="請再次輸入密碼"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  required
                  disabled={isLoading}
                  minLength={8}
                />
              </div>
            </div>

            <CardFooter className="flex-col gap-3 px-0 pt-6">
              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? '註冊中...' : '註冊'}
              </Button>
              
              <div className="text-sm text-center text-muted-foreground">
                已經有帳號了？{' '}
                <Link href="/login" className="text-primary hover:underline font-medium">
                  立即登入
                </Link>
              </div>
            </CardFooter>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
