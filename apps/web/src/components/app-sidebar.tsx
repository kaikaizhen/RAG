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
  MessageSquare,
  Settings,
  Trash2,
  Palette,
} from "lucide-react"
import * as React from "react"
import { useTheme } from "@/contexts/theme-context"
import { ThemeColorPicker } from "@/components/theme-color-picker"
import { Separator } from "@/components/ui/separator"
import { SpaceSwitcher } from "@/components/space-switcher"
import { Logo } from "@/components/logo"
import { useLongPress } from "@/hooks/use-long-press"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { canWriteKnowledge, getClient, getProfile, signOut } from "@/lib/rag"

interface ChatHistory {
  id: string
  name: string
  createdAt?: string
  updateTime?: string
  createTime?: string
}

interface ChatRowProps {
  chat: ChatHistory
  expanded: boolean
  timeText: string
  onOpen: (chatId: string) => void
  onDelete: (chat: ChatHistory) => void
}

/** 單筆對話：點一下開啟；觸控裝置長按刪除（沒有 hover），桌面滑過會出現垃圾桶 */
function ChatRow({ chat, expanded, timeText, onOpen, onDelete }: ChatRowProps) {
  const longPress = useLongPress(() => onDelete(chat))
  return (
    <SidebarMenuItem>
      <SidebarMenuButton
        {...longPress}
        onClick={() => onOpen(chat.id)}
        tooltip={chat.name}
        className="group select-none [-webkit-touch-callout:none]"
      >
        <MessageSquare className="h-4 w-4 flex-shrink-0" />
        <div className="flex flex-1 flex-col gap-0.5 min-w-0">
          <span className="truncate text-sm">{chat.name}</span>
          {timeText && <span className="truncate text-xs text-muted-foreground">{timeText}</span>}
        </div>
        {expanded && (
          <div
            role="button"
            tabIndex={0}
            className="inline-flex items-center justify-center h-6 w-6 rounded-md opacity-0 group-hover:opacity-100 focus-visible:opacity-100 transition-opacity flex-shrink-0 hover:bg-accent hover:text-accent-foreground cursor-pointer [@media(hover:none)]:hidden"
            onClick={(e) => {
              e.stopPropagation()
              onDelete(chat)
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault()
                e.stopPropagation()
                onDelete(chat)
              }
            }}
            title="刪除對話"
          >
            <Trash2 className="h-3 w-3" />
          </div>
        )}
      </SidebarMenuButton>
    </SidebarMenuItem>
  )
}

export function AppSidebar() {
  const { state } = useSidebar()
  const { theme, toggleTheme } = useTheme()
  const [userName, setUserName] = React.useState<string>("使用者")
  const [userInitial, setUserInitial] = React.useState<string>("使")
  const [userRole, setUserRole] = React.useState<string>("使用者")
  const [recentChats, setRecentChats] = React.useState<ChatHistory[]>([])
  // 管理入口依目前 Space 的 knowledge.write 權限顯示（Space 切換後重新判斷）
  const [canAccessSettings, setCanAccessSettings] = React.useState(false)
  const [pendingDelete, setPendingDelete] = React.useState<ChatHistory | null>(null)
  
  React.useEffect(() => {
    const profile = getProfile()
    if (profile?.name) {
      setUserName(profile.name)
      setUserInitial(profile.name.charAt(0))
    }
    if (profile?.manage) {
      setUserRole(profile.manage)
    }

    // 載入聊天記錄
    loadChatHistory()
    
    // 監聽使用者資料更新事件
    const handleUserDataUpdate = (event: CustomEvent) => {
      const { userName: newUserName } = event.detail
      if (newUserName) {
        setUserName(newUserName)
        setUserInitial(newUserName.charAt(0))
      }
    }
    
    // 監聽聊天記錄更新事件
    const handleChatHistoryUpdate = () => {
      loadChatHistory()
    }
    
    window.addEventListener('userDataUpdated', handleUserDataUpdate as EventListener)
    window.addEventListener('chatHistoryUpdated', handleChatHistoryUpdate)
    canWriteKnowledge().then(setCanAccessSettings)
    const offSpace = getClient().on('space-changed', () => {
      canWriteKnowledge().then(setCanAccessSettings)
      setRecentChats([])
      loadChatHistory()
      window.dispatchEvent(new CustomEvent('startNewChat'))
    })
    
    return () => {
      offSpace()
      window.removeEventListener('userDataUpdated', handleUserDataUpdate as EventListener)
      window.removeEventListener('chatHistoryUpdated', handleChatHistoryUpdate)
    }
  }, [])

  // 載入聊天記錄（範圍由伺服器依登入者與目前 Space 決定）
  const loadChatHistory = async () => {
    try {
      const data = await getClient().chat.history()
      setRecentChats(data.filter((chat): chat is ChatHistory => !!chat.id && !!chat.name))
    } catch (error) {
      console.error("載入聊天記錄失敗:", error)
    }
  }

  const handleLogout = () => {
    void signOut()
  }

  const handleProfile = () => {
    window.location.href = "/profile"
  }

  const handleNewChat = () => {
    // 觸發新對話事件
    window.dispatchEvent(new CustomEvent('startNewChat'))
  }

  const handleSettings = () => {
    window.location.href = "/admin-settings"
  }

  const handleChatClick = (chatId: string) => {
    console.log("點擊聊天記錄，ID:", chatId)
    // 觸發載入聊天事件
    window.dispatchEvent(new CustomEvent('loadChat', { detail: { chatId } }))
  }

  const confirmDeleteChat = async () => {
    const chat = pendingDelete
    setPendingDelete(null)
    if (!chat) return
    try {
      await getClient().chat.delete(chat.id)
      setRecentChats(prev => prev.filter(c => c.id !== chat.id))
      window.dispatchEvent(new CustomEvent('chatDeleted', { detail: { chatId: chat.id } }))
    } catch (error) {
      console.error("刪除聊天失敗:", error)
    }
  }

  
  const formatTime = (dateString?: string) => {
    if (!dateString) return ""
    
    const date = new Date(dateString)
    const now = new Date()
    const diffInMs = now.getTime() - date.getTime()
    const diffInHours = Math.floor(diffInMs / (1000 * 60 * 60))
    const diffInDays = Math.floor(diffInMs / (1000 * 60 * 60 * 24))
    
    if (diffInHours < 1) {
      return "剛才"
    } else if (diffInHours < 24) {
      return `${diffInHours} 小時前`
    } else if (diffInDays === 1) {
      return "昨天"
    } else if (diffInDays < 7) {
      return `${diffInDays} 天前`
    } else {
      return date.toLocaleDateString("zh-TW")
    }
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
              {canAccessSettings && (
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7"
                  onClick={handleSettings}
                  title="設定"
                >
                  <Settings className="h-5 w-5" />
                </Button>
              )}
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

        {/* 新對話按鈕 */}
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton onClick={handleNewChat} tooltip="新對話">
                  <Plus className="h-4 w-4" />
                  <span>新對話</span>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>

        {/* 最近的對話 */}
        <SidebarGroup>
          <SidebarGroupLabel>
            最近的對話
            <span className="ml-2 hidden text-xs font-normal text-muted-foreground [@media(hover:none)]:inline">長按可刪除</span>
          </SidebarGroupLabel>
          <SidebarGroupContent>
            {recentChats.length === 0 ? (
              <div className="px-2 py-4 text-center text-sm text-muted-foreground">
                尚無對話記錄
              </div>
            ) : (
              <SidebarMenu>
                {recentChats.map((chat) => (
                  <ChatRow
                    key={chat.id}
                    chat={chat}
                    expanded={state === "expanded"}
                    timeText={formatTime(chat.createdAt)}
                    onOpen={handleChatClick}
                    onDelete={setPendingDelete}
                  />
                ))}
              </SidebarMenu>
            )}
          </SidebarGroupContent>
        </SidebarGroup>
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
      <AlertDialog open={pendingDelete !== null} onOpenChange={(open) => !open && setPendingDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>刪除這個對話？</AlertDialogTitle>
            <AlertDialogDescription className="break-words">
              「{pendingDelete?.name}」的所有訊息都會被刪除，無法復原。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction onClick={confirmDeleteChat}>刪除</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Sidebar>
  )
}
