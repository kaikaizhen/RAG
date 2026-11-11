"use client"

import * as React from "react"
import { Building2, Check, ChevronsUpDown, Plus } from "lucide-react"
import type { Space } from "@rag/client-sdk"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { SidebarMenu, SidebarMenuButton, SidebarMenuItem, useSidebar } from "@/components/ui/sidebar"
import { getClient } from "@/lib/rag"
import { CreateSpaceDialog, SPACES_UPDATED_EVENT } from "@/components/create-space-dialog"

/**
 * 目前 Space 與切換。選擇只是 hint，授權由伺服器決定；
 * 切換後 SDK 會清除 Space 相關快取，頁面監聽 space-changed 重新載入。
 */
export function SpaceSwitcher() {
  const { state } = useSidebar()
  const [spaces, setSpaces] = React.useState<Space[]>([])
  const [selected, setSelected] = React.useState<number | null>(null)
  const [creating, setCreating] = React.useState(false)

  React.useEffect(() => {
    const rag = getClient()
    let alive = true
    const load = () =>
      rag.spaces
        .list()
        .then((list) => {
          if (!alive) return
          setSpaces(list)
          if (rag.spaces.selected() === null && list.length > 0) {
            rag.spaces.select((list.find((s) => s.isDefault) ?? list[0]).spaceID ?? null)
          }
          setSelected(rag.spaces.selected())
        })
        .catch(() => {
          /* 無 Space 時側邊欄不顯示切換器，實際錯誤由各頁面的 API 呼叫呈現 */
        })
    void load()
    const off = rag.on("space-changed", ({ spaceId }) => {
      setSelected(spaceId)
      void load() // 切換後 SDK 已清快取：重新取得清單，新建立的空間也會出現
    })
    window.addEventListener(SPACES_UPDATED_EVENT, load)
    return () => {
      alive = false
      off()
      window.removeEventListener(SPACES_UPDATED_EVENT, load)
    }
  }, [])

  const current = spaces.find((s) => s.spaceID === selected)
  if (spaces.length === 0) return null

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton tooltip={current?.name ?? "選擇空間"}>
              <Building2 className="h-4 w-4" />
              <span className="truncate">{current?.name ?? "選擇空間"}</span>
              <ChevronsUpDown className="ml-auto size-4" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="min-w-56" side={state === "collapsed" ? "right" : "bottom"}>
            <DropdownMenuLabel>切換空間</DropdownMenuLabel>
            <DropdownMenuSeparator />
            {spaces.map((space) => (
              <DropdownMenuItem key={space.spaceID} onClick={() => getClient().spaces.select(space.spaceID ?? null)}>
                <span className="flex-1 truncate">{space.name}</span>
                <span className="text-xs text-muted-foreground">{space.role}</span>
                {space.spaceID === selected && <Check className="ml-2 h-4 w-4" />}
              </DropdownMenuItem>
            ))}
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => setCreating(true)}>
              <Plus className="mr-2 h-4 w-4" />
              建立新空間
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
      <CreateSpaceDialog open={creating} onOpenChange={setCreating} />
    </SidebarMenu>
  )
}
