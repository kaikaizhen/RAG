"use client"

import * as React from "react"
import { Building2, Check, ChevronsUpDown } from "lucide-react"
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

/**
 * 目前 Space 與切換。選擇只是 hint，授權由伺服器決定；
 * 切換後 SDK 會清除 Space 相關快取，頁面監聽 space-changed 重新載入。
 */
export function SpaceSwitcher() {
  const { state } = useSidebar()
  const [spaces, setSpaces] = React.useState<Space[]>([])
  const [selected, setSelected] = React.useState<number | null>(null)

  React.useEffect(() => {
    const rag = getClient()
    let alive = true
    rag.spaces
      .list()
      .then(async (list) => {
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
    const off = rag.on("space-changed", ({ spaceId }) => setSelected(spaceId))
    return () => {
      alive = false
      off()
    }
  }, [])

  const current = spaces.find((s) => s.spaceID === selected)
  if (spaces.length === 0) return null

  // 只有一個 Space 時僅顯示名稱，不提供切換
  if (spaces.length === 1) {
    return state === "expanded" ? (
      <div className="flex items-center gap-2 px-3 py-1.5 text-xs text-muted-foreground">
        <Building2 className="h-3.5 w-3.5" />
        <span className="truncate">{spaces[0].name}</span>
      </div>
    ) : null
  }

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
          <DropdownMenuContent align="start" className="min-w-56">
            <DropdownMenuLabel>切換空間</DropdownMenuLabel>
            <DropdownMenuSeparator />
            {spaces.map((space) => (
              <DropdownMenuItem key={space.spaceID} onClick={() => getClient().spaces.select(space.spaceID ?? null)}>
                <span className="flex-1 truncate">{space.name}</span>
                <span className="text-xs text-muted-foreground">{space.role}</span>
                {space.spaceID === selected && <Check className="ml-2 h-4 w-4" />}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}
