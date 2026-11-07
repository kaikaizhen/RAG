"use client"

import * as React from "react"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Search, X, Plus } from "lucide-react"
import { Checkbox } from "@/components/ui/checkbox"

interface HistorySearchProps {
  searchQuery: string
  onSearchChange: (query: string) => void
  totalCount: number
  filteredCount: number
  onNewClick?: () => void
  // 多選相關
  selectedCount: number
  showSelection: boolean
  onToggleSelection: () => void
  onSelectAll: (checked: boolean) => void
  onDeleteSelected: () => void
}

export function HistorySearch({
  searchQuery,
  onSearchChange,
  totalCount,
  filteredCount,
  onNewClick,
  selectedCount,
  showSelection,
  onToggleSelection,
  onSelectAll,
  onDeleteSelected,
}: HistorySearchProps) {
  return (
    <div className="space-y-4">
      {/* 標題列 */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold">問答紀錄</h2>
        </div>
        {onNewClick && (
          <Button onClick={onNewClick}>
            <Plus className="h-4 w-4 mr-2" />
            新增問答
          </Button>
        )}
      </div>

      {/* 搜尋框 */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground z-10" />
        <Input
          type="text"
          placeholder="搜尋問題或答案..."
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          className="pl-10 pr-10"
        />
        {searchQuery && (
          <Button
            variant="ghost"
            size="sm"
            className="absolute right-1 top-1/2 h-7 w-7 -translate-y-1/2 p-0 z-10"
            onClick={() => onSearchChange("")}
          >
            <X className="h-4 w-4" />
          </Button>
        )}
      </div>

      {/* 多選工具列 */}
      {showSelection ? (
        <div className="flex items-center gap-3 py-2">
          <div className="flex items-center gap-2">
            <Checkbox
              checked={selectedCount > 0 && selectedCount === filteredCount}
              onCheckedChange={onSelectAll}
            />
            <span className="text-sm text-muted-foreground">
              {selectedCount} selected
            </span>
          </div>
          
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={onDeleteSelected}
              disabled={selectedCount === 0}
              className="h-8"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M3 6h18" />
                <path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6" />
                <path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2" />
              </svg>
            </Button>
          </div>

          <Button
            variant="ghost"
            size="sm"
            onClick={onToggleSelection}
            className="ml-auto h-8"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      ) : (
        <div className="flex items-center justify-between py-2">
          <div className="text-sm text-muted-foreground">
            共 {totalCount} 筆問答
            {searchQuery && `，符合 ${filteredCount} 筆`}
          </div>
          <Button
            variant="link"
            size="sm"
            onClick={onToggleSelection}
            className="h-auto p-0 text-sm"
          >
            選取
          </Button>
        </div>
      )}
    </div>
  )
}
