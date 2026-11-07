"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { ArrowLeft, Camera, Save, Loader2 } from "lucide-react"
import { describeError, getClient, getProfile, setProfile } from "@/lib/rag"

export default function ProfilePage() {
  const router = useRouter()
  const [isLoading, setIsLoading] = React.useState(false)
  const [isFetching, setIsFetching] = React.useState(true)
  const [error, setError] = React.useState("")
  const [success, setSuccess] = React.useState("")

  // 使用者資料
  const [userId, setUserId] = React.useState("")
  const [account, setAccount] = React.useState("")
  const [name, setName] = React.useState("")
  const [currentPassword, setCurrentPassword] = React.useState("")
  const [newPassword, setNewPassword] = React.useState("")
  const [confirmPassword, setConfirmPassword] = React.useState("")
  const [userRole, setUserRole] = React.useState("")
  const [userInitial, setUserInitial] = React.useState("使")

  // 從 API 載入使用者資料（後端只允許讀取本人）
  React.useEffect(() => {
    const loadUserData = async () => {
      try {
        const profile = getProfile()
        if (!profile) {
          router.push("/login")
          return
        }

        setUserId(String(profile.userId))
        setUserRole(profile.manage || "使用者")
        setName(profile.name)
        setUserInitial(profile.name.charAt(0) || "使")

        const data = await getClient().users.get(profile.userId)
        setAccount(data.account || "")

        // 優先順序：nameZh > nameEn > nameOther
        const displayName = data.nameZh || data.nameEn || data.nameOther || profile.name
        setName(displayName)
        setUserInitial(displayName.charAt(0) || "使")
      } catch (err) {
        console.error("載入使用者資料失敗:", err)
        setError(describeError(err, "載入使用者資料失敗"))
      } finally {
        setIsFetching(false)
      }
    }

    loadUserData()
  }, [router])

  // 儲存更新
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsLoading(true)
    setError("")
    setSuccess("")

    if (!name.trim()) {
      setError("名稱不能為空")
      setIsLoading(false)
      return
    }

    if (!currentPassword.trim()) {
      setError("請輸入當前密碼以驗證身份")
      setIsLoading(false)
      return
    }

    // 如果使用者想要修改密碼，需要驗證新密碼
    if (newPassword || confirmPassword) {
      if (newPassword !== confirmPassword) {
        setError("新密碼與確認密碼不一致")
        setIsLoading(false)
        return
      }
      if (newPassword.length < 6) {
        setError("新密碼長度至少需要 6 個字元")
        setIsLoading(false)
        return
      }
    }

    try {
      const rag = getClient()
      const id = parseInt(userId)
      // 取得完整資料後整筆更新；manage / isActive 由伺服器保留，不接受前端變更
      const userData = await rag.users.get(id)

      await rag.users.update({
        id,
        account: userData.account,
        password: newPassword.trim() ? newPassword : currentPassword,
        nameZh: name,
        nameEn: name,
        nameOther: name,
        manage: userData.manage,
        isActive: userData.isActive,
      })

      // 更新顯示用資料並通知其他組件
      const profile = getProfile()
      if (profile) setProfile({ ...profile, name })
      window.dispatchEvent(new CustomEvent('userDataUpdated', {
        detail: { userName: name }
      }))

      // 清空密碼欄位
      setCurrentPassword("")
      setNewPassword("")
      setConfirmPassword("")

      setSuccess("個人資料已成功更新！" + (newPassword ? " 密碼也已更新。" : ""))

      // 3 秒後清除成功訊息
      setTimeout(() => {
        setSuccess("")
      }, 3000)

    } catch (err) {
      setError(describeError(err, "更新失敗，請稍後再試"))
      console.error("更新錯誤:", err)
    } finally {
      setIsLoading(false)
    }
  }

  if (isFetching) {
    return (
      <div className="flex h-dvh w-full items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="container mx-auto max-w-2xl p-4 py-8">
      {/* 返回按鈕 */}
      <Button
        variant="ghost"
        size="sm"
        className="mb-4"
        onClick={() => router.push("/")}
      >
        <ArrowLeft className="mr-2 h-4 w-4" />
        返回
      </Button>

      <Card>
        <CardHeader>
          <CardTitle>個人資料</CardTitle>
          <CardDescription>編輯您的個人資訊</CardDescription>
        </CardHeader>

        <form onSubmit={handleSubmit}>
          <CardContent className="space-y-6">
            {/* 頭像區域 */}
            <div className="flex items-center gap-4">
              <Avatar className="h-20 w-20">
                <AvatarFallback className="text-2xl">{userInitial}</AvatarFallback>
              </Avatar>
              <div className="flex-1">
                <p className="text-sm font-medium">個人頭像</p>
                <p className="text-xs text-muted-foreground mb-2">
                  目前顯示名稱的第一個字
                </p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled
                >
                  <Camera className="mr-2 h-4 w-4" />
                  上傳照片（即將推出）
                </Button>
              </div>
            </div>

            {/* 帳號（唯讀） */}
            <div className="space-y-2">
              <Label htmlFor="account">帳號</Label>
              <Input
                id="account"
                value={account}
                disabled
                className="bg-muted"
              />
              <p className="text-xs text-muted-foreground">
                帳號無法修改
              </p>
            </div>

            {/* 身份（唯讀） */}
            <div className="space-y-2">
              <Label htmlFor="role">身份</Label>
              <Input
                id="role"
                value={userRole}
                disabled
                className="bg-muted"
              />
            </div>

            {/* 名稱 */}
            <div className="space-y-2">
              <Label htmlFor="name">名稱 *</Label>
              <Input
                id="name"
                value={name}
                onChange={(e) => {
                  setName(e.target.value)
                  if (e.target.value) {
                    setUserInitial(e.target.value.charAt(0))
                  }
                }}
                placeholder="請輸入您的名稱"
                required
              />
            </div>

            {/* 當前密碼 */}
            <div className="space-y-2">
              <Label htmlFor="currentPassword">當前密碼 *</Label>
              <Input
                id="currentPassword"
                type="password"
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                placeholder="請輸入當前密碼以驗證身份"
                required
              />
              <p className="text-xs text-muted-foreground">
                為了安全性，更新個人資料需要驗證您的密碼
              </p>
            </div>

            {/* 分隔線 */}
            <div className="border-t pt-4">
              <h3 className="text-sm font-semibold mb-4">修改密碼（選填）</h3>
              
              {/* 新密碼 */}
              <div className="space-y-2 mb-4">
                <Label htmlFor="newPassword">新密碼</Label>
                <Input
                  id="newPassword"
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="如果要修改密碼請輸入新密碼"
                />
                <p className="text-xs text-muted-foreground">
                  密碼長度至少 6 個字元
                </p>
              </div>

              {/* 確認新密碼 */}
              <div className="space-y-2">
                <Label htmlFor="confirmPassword">確認新密碼</Label>
                <Input
                  id="confirmPassword"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="再次輸入新密碼"
                  disabled={!newPassword}
                />
              </div>
            </div>

            {/* 錯誤訊息 */}
            {error && (
              <div className="rounded-lg bg-red-50 p-3 text-sm text-red-600 dark:bg-red-950 dark:text-red-400">
                {error}
              </div>
            )}

            {/* 成功訊息 */}
            {success && (
              <div className="rounded-lg bg-green-50 p-3 text-sm text-green-600 dark:bg-green-950 dark:text-green-400">
                {success}
              </div>
            )}
          </CardContent>

          <CardFooter className="flex justify-between pt-3">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push("/")}
              disabled={isLoading}
            >
              取消
            </Button>
            <Button type="submit" disabled={isLoading}>
              {isLoading ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  儲存中...
                </>
              ) : (
                <>
                  <Save className="mr-2 h-4 w-4" />
                  儲存變更
                </>
              )}
            </Button>
          </CardFooter>
        </form>
      </Card>
    </div>
  )
}
