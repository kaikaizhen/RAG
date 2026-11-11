"use client"

import * as React from "react"
import { BookOpen, Code2, ListChecks, LogOut, Moon, Palette, User, Users } from "lucide-react"
import type { Space } from "@rag/client-sdk"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { MobileGroup, MobileRow, MobileTitle } from "@/components/mobile/list"
import { ThemeColorPicker } from "@/components/theme-color-picker"
import { useTheme } from "@/contexts/theme-context"
import { canWriteKnowledge, getClient, getProfile, signOut } from "@/lib/rag"
import { cn } from "@/lib/utils"

/**
 * 「我的」：手機版把側邊欄裡的帳號、空間、管理入口、外觀設定集中在這一頁（桌面版仍用側邊欄）。
 * 空間選擇只是 hint，授權一律由伺服器決定；管理入口依目前空間的 knowledge.write 權限顯示。
 */
export default function MePage() {
  const { theme, toggleTheme } = useTheme()
  const [profile, setProfile] = React.useState<ReturnType<typeof getProfile>>(null)
  const [spaces, setSpaces] = React.useState<Space[]>([])
  const [selected, setSelected] = React.useState<number | null>(null)
  const [canManage, setCanManage] = React.useState(false)
  const [showColors, setShowColors] = React.useState(false)

  React.useEffect(() => {
    const rag = getClient()
    setProfile(getProfile())
    rag.spaces.list().then((list) => {
      setSpaces(list)
      setSelected(rag.spaces.selected())
    }).catch(() => setSpaces([]))
    canWriteKnowledge().then(setCanManage)
    return rag.on("space-changed", ({ spaceId }) => {
      setSelected(spaceId)
      canWriteKnowledge().then(setCanManage)
    })
  }, [])

  const name = profile?.name ?? "使用者"

  return (
    <div className="mx-auto max-w-2xl space-y-6 px-4 pb-8 pt-6 md:p-8">
      <MobileTitle title="我的" />
      <h1 className="hidden text-2xl font-bold md:block">我的</h1>

      <MobileGroup>
        <MobileRow
          href="/profile"
          label={name}
          value={profile?.manage}
          icon={
            <Avatar className="h-9 w-9">
              <AvatarFallback>{name.charAt(0)}</AvatarFallback>
            </Avatar>
          }
        />
      </MobileGroup>

      {spaces.length > 1 && (
        <MobileGroup label="空間">
          {spaces.map((space) => (
            <MobileRow
              key={space.spaceID}
              icon={<Users className="h-5 w-5" />}
              label={space.name ?? `空間 ${space.spaceID}`}
              value={space.role}
              selected={space.spaceID === selected}
              onClick={() => space.spaceID != null && getClient().spaces.select(space.spaceID)}
            />
          ))}
        </MobileGroup>
      )}

      {canManage && (
        <MobileGroup label="知識庫">
          <MobileRow href="/admin-settings" icon={<BookOpen className="h-5 w-5" />} label="新增與管理問答" />
          <MobileRow href="/admin-settings/history" icon={<ListChecks className="h-5 w-5" />} label="問答紀錄" />
        </MobileGroup>
      )}

      <MobileGroup label="開發">
        <MobileRow href="/developer" icon={<Code2 className="h-5 w-5" />} label="開發者" value="權杖、Webhook" />
      </MobileGroup>

      <MobileGroup label="外觀">
        <MobileRow
          icon={<Moon className="h-5 w-5" />}
          label="深色模式"
          onClick={toggleTheme}
          trailing={
            <span
              role="switch"
              aria-checked={theme === "dark"}
              aria-label="深色模式"
              className={cn("flex h-7 w-12 items-center rounded-full p-0.5 transition-colors", theme === "dark" ? "justify-end bg-primary" : "justify-start bg-muted")}
            >
              <span className="h-6 w-6 rounded-full bg-background shadow-sm" />
            </span>
          }
        />
        <MobileRow icon={<Palette className="h-5 w-5" />} label="主題色" onClick={() => setShowColors((v) => !v)} />
        {showColors && (
          <div className="p-4">
            <ThemeColorPicker />
          </div>
        )}
      </MobileGroup>

      <Button variant="outline" className="h-13 w-full rounded-full text-base font-semibold text-destructive" onClick={() => void signOut()}>
        <LogOut className="mr-2 h-5 w-5" />
        登出
      </Button>

      <p className="text-center text-xs text-muted-foreground">
        <User className="mr-1 inline h-3 w-3" />
        登入保持 30 天，重新開啟 App 不需要再登入
      </p>
    </div>
  )
}
