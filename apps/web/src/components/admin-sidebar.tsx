"use client"

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarTrigger,
  useSidebar,
} from "@/components/ui/sidebar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
} from "@/components/ui/dropdown-menu"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import {
  ChevronsUpDown,
  LogOut,
  User,
  CreditCard,
  Plus,
  FolderPlus,
  Settings,
  BookOpen,
  Palette,
} from "lucide-react"
import * as React from "react"
import { usePathname, useRouter } from "next/navigation"
import { useTheme } from "@/contexts/theme-context"
import { ThemeColorPicker } from "@/components/theme-color-picker"
import { SpaceSwitcher } from "@/components/space-switcher"
import { Logo } from "@/components/logo"
import { getClient, getProfile, signOut } from "@/lib/rag"

interface QAPair {
  id: string
  question: string
  typeName: string
  createdAt: string
}

const SIDEBAR_LIMIT = 20

export function AdminSidebar() {
  const { state } = useSidebar()
  const pathname = usePathname()
  const router = useRouter()
  const { theme } = useTheme()
  const [userName, setUserName] = React.useState<string>("使用者")
  const [userInitial, setUserInitial] = React.useState<string>("使")
  const [userRole, setUserRole] = React.useState<string>("使用者")
  const [adminQAs, setAdminQAs] = React.useState<QAPair[]>([])
  
  React.useEffect(() => {
    const profile = getProfile()
    if (profile?.name) {
      setUserName(profile.name)
      setUserInitial(profile.name.charAt(0))
    }
    if (profile?.manage) {
      setUserRole(profile.manage)
    }

    loadAdminQAs()
    
    // 監聽使用者資料更新事件
    const handleUserDataUpdate = (event: CustomEvent) => {
      const { userName: newUserName } = event.detail
      if (newUserName) {
        setUserName(newUserName)
        setUserInitial(newUserName.charAt(0))
      }
    }
    
    // 監聽管理員 Q&A 更新事件
    const handleAdminQAUpdate = () => {
      loadAdminQAs()
    }
    
    window.addEventListener('userDataUpdated', handleUserDataUpdate as EventListener)
    window.addEventListener('adminQAUpdated', handleAdminQAUpdate)
    const offSpace = getClient().on('space-changed', () => {
      setAdminQAs([])
      loadAdminQAs()
    })
    
    return () => {
      offSpace()
      window.removeEventListener('userDataUpdated', handleUserDataUpdate as EventListener)
      window.removeEventListener('adminQAUpdated', handleAdminQAUpdate)
    }
  }, [])

  // 直接向 API 取得目前 Space 的知識（不再快取在 localStorage，避免跨帳號 / 跨 Space 殘留）
  const loadAdminQAs = async () => {
    try {
      const rag = getClient()
      const [questions, items] = await Promise.all([rag.questions.list(), rag.qaTypeItems.list()])
      const itemName = new Map(items.map((item) => [item.id, item.nameZh || item.nameEn || item.nameOther || `細項 ${item.id}`]))
      setAdminQAs(
        questions
          .filter((q) => q.status !== "Superseded")
          .sort((a, b) => b.createAt.localeCompare(a.createAt))
          .slice(0, SIDEBAR_LIMIT)
          .map((q) => ({
            id: String(q.id),
            question: q.question,
            typeName: itemName.get(q.qaItemTypeID) ?? `細項 ${q.qaItemTypeID}`,
            createdAt: q.createAt,
          })),
      )
    } catch (error) {
      console.error("載入管理員 Q&A 失敗:", error)
    }
  }

  const handleLogout = () => {
    void signOut()
  }

  const handleProfile = () => {
    window.location.href = "/profile"
  }

  const handleNewChat = () => {
    // 返回到管理設定主頁
    router.push("/admin-settings")
  }

  const handleChatProject = () => {
    // 前往歷史紀錄頁面
    router.push("/admin-settings/history")
  }

  const handleQAClick = (qa: QAPair) => {
    console.log("點擊 Q&A:", qa)
    // 可以在這裡加上點擊 Q&A 的邏輯
  }

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <div className={`flex items-center gap-2 py-2 ${state === "collapsed" ? "justify-center px-2" : "pl-0"}`}>
          <SidebarTrigger className="h-7 w-7" />
          {state === "expanded" && (
            <>
              <button 
                onClick={() => window.location.href = "/"}
                className="flex flex-1 items-center gap-2 text-left text-lg font-semibold hover:text-primary transition-colors cursor-pointer"
              >
                <Logo size={28} />
                RAGence
              </button>
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7"
                onClick={() => router.push("/admin-settings")}
                title="設定"
              >
                <Settings className="h-5 w-5" />
              </Button>
            </>
          )}
        </div>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <SpaceSwitcher />
          </SidebarGroupContent>
        </SidebarGroup>

        {/* 管理功能按鈕 */}
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton onClick={handleNewChat} tooltip="管理設定">
                  <Plus className="h-4 w-4" />
                  <span>新增問答</span>
                </SidebarMenuButton>
              </SidebarMenuItem>
              <SidebarMenuItem>
                <SidebarMenuButton onClick={handleChatProject} tooltip="歷史紀錄">
                  <FolderPlus className="h-4 w-4" />
                  <span>問答紀錄</span>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>

        {/* 管理員 Q&A 群組 */}
        {adminQAs.length > 0 && (
          <SidebarGroup>
            <SidebarGroupLabel>管理 Q&A</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {adminQAs.map((qa) => (
                  <SidebarMenuItem key={qa.id}>
                    <SidebarMenuButton 
                      onClick={() => handleQAClick(qa)}
                      tooltip={qa.question}
                    >
                      <BookOpen className="h-4 w-4" />
                      <div className="flex flex-1 flex-col gap-0.5">
                        <span className="truncate text-sm">{qa.question}</span>
                        <span className="truncate text-xs text-muted-foreground">
                          {qa.typeName} • {new Date(qa.createdAt).toLocaleDateString("zh-TW")}
                        </span>
                      </div>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        )}
      </SidebarContent>

      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton
                  size="lg"
                  className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
                >
                  <Avatar className="h-8 w-8 rounded-lg">
                    <AvatarFallback className="rounded-lg">{userInitial}</AvatarFallback>
                  </Avatar>
                  {state === "expanded" && (
                    <>
                      <div className="grid flex-1 text-left text-sm leading-tight">
                        <span className="truncate font-semibold">{userName}</span>
                        <span className="truncate text-xs">RAGence {userRole}</span>
                      </div>
                      <ChevronsUpDown className="ml-auto size-4" />
                    </>
                  )}
                </SidebarMenuButton>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                className="w-[--radix-dropdown-menu-trigger-width] min-w-56 rounded-lg"
                side="bottom"
                align="end"
                sideOffset={4}
              >
                <DropdownMenuLabel className="p-0 font-normal">
                  <div className="flex items-center gap-2 px-1 py-1.5 text-left text-sm">
                    <Avatar className="h-8 w-8 rounded-lg">
                      <AvatarFallback className="rounded-lg">{userInitial}</AvatarFallback>
                    </Avatar>
                    <div className="grid flex-1 text-left text-sm leading-tight">
                      <span className="truncate font-semibold">{userName}</span>
                      <span className="truncate text-xs">RAGence {userRole}</span>
                    </div>
                  </div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuGroup>
                  <DropdownMenuItem onClick={handleProfile}>
                    <User />
                    個人資料
                  </DropdownMenuItem>
                  <DropdownMenuItem>
                    <CreditCard />
                    帳戶設定
                  </DropdownMenuItem>
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      <Palette />
                      選擇主題色
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="w-64">
                      <div className="p-2">
                        <ThemeColorPicker />
                      </div>
                    </DropdownMenuSubContent>
                  </DropdownMenuSub>
                </DropdownMenuGroup>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={handleLogout}>
                  <LogOut />
                  登出
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
    </Sidebar>
  )
}
