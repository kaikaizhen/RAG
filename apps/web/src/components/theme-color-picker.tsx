"use client"

import * as React from "react"
import { Check, Sun, Moon } from "lucide-react"
import { cn } from "@/lib/utils"
import { themeColors } from "@/lib/theme-colors"
import { useTheme } from "@/contexts/theme-context"

export function ThemeColorPicker() {
  const { theme, themeColor, setThemeColor, toggleTheme } = useTheme()

  return (
    <div className="space-y-3">
      {/* 深色/淺色模式切換按鈕 */}
      <div className="flex items-center justify-center">
        <button
          onClick={toggleTheme}
          className={cn(
            "flex h-7 w-14 items-center rounded-full transition-colors",
            "focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
            theme === "dark" ? "bg-primary justify-end" : "bg-muted justify-start"
          )}
          title={theme === "light" ? "切換到深色模式" : "切換到淺色模式"}
        >
          <div className="mx-0.5 flex h-6 w-6 items-center justify-center rounded-full bg-background shadow-sm">
            {theme === "light" ? (
              <Sun className="h-3.5 w-3.5 text-foreground" />
            ) : (
              <Moon className="h-3.5 w-3.5 text-foreground" />
            )}
          </div>
        </button>
      </div>

      {/* 顏色選擇器 */}
      <div className="grid grid-cols-5 gap-2 px-2">
        {themeColors.map((color) => {
          // 根據當前主題選擇對應的顏色
          const vars = theme === "light" ? color.lightVars : color.darkVars

          return (
            <button
              key={color.value}
              onClick={() => setThemeColor(color.value)}
              className={cn(
                "relative h-8 w-8 rounded-full transition-all hover:scale-110",
                "focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
                themeColor === color.value && "ring-2 ring-offset-2 ring-primary scale-110"
              )}
              style={{
                background: vars.sidebar,
                border: `2px solid ${vars.primary}`,
              }}
              title={color.name}
            >
              {themeColor === color.value && (
                <div className="absolute inset-0 flex items-center justify-center">
                  <Check className="h-4 w-4 drop-shadow-md" style={{ color: vars.primary }} />
                </div>
              )}
            </button>
          )
        })}
      </div>
    </div>
  )
}
