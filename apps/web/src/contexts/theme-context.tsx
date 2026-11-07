"use client"

import * as React from "react"
import { themeColors, type ThemeColor } from "@/lib/theme-colors"

interface ThemeContextType {
  theme: "light" | "dark"
  themeColor: string
  setTheme: (theme: "light" | "dark") => void
  setThemeColor: (color: string) => void
  toggleTheme: () => void
}

const ThemeContext = React.createContext<ThemeContextType | undefined>(undefined)

export function useTheme() {
  const context = React.useContext(ThemeContext)
  if (!context) {
    throw new Error("useTheme must be used within a ThemeProvider")
  }
  return context
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setThemeState] = React.useState<"light" | "dark">("light")
  const [themeColor, setThemeColorState] = React.useState<string>("gray")

  React.useEffect(() => {
    // 從 localStorage 讀取主題設定
    const storedTheme = localStorage.getItem("theme") as "light" | "dark" | null
    const storedColor = localStorage.getItem("themeColor") || "gray"

    if (storedTheme) {
      setThemeState(storedTheme)
    }
    setThemeColorState(storedColor)

    // 應用主題
    applyTheme(storedTheme || "light", storedColor)
  }, [])

  const applyTheme = (newTheme: "light" | "dark", color: string) => {
    const root = document.documentElement

    // 切換 dark class
    root.classList.toggle("dark", newTheme === "dark")

    // 找到對應的顏色配置
    const colorConfig = themeColors.find((c) => c.value === color)
    if (!colorConfig) return

    // 應用顏色變數
    const vars = newTheme === "light" ? colorConfig.lightVars : colorConfig.darkVars

    // 主要顏色
    root.style.setProperty("--background", vars.background)
    root.style.setProperty("--foreground", vars.foreground)
    root.style.setProperty("--primary", vars.primary)
    root.style.setProperty("--primary-foreground", vars.primaryForeground)
    root.style.setProperty("--accent", vars.accent)
    root.style.setProperty("--accent-foreground", vars.accentForeground)

    // Sidebar 顏色
    root.style.setProperty("--sidebar", vars.sidebar)
    root.style.setProperty("--sidebar-foreground", vars.sidebarForeground)
    root.style.setProperty("--sidebar-accent", vars.sidebarAccent)
    root.style.setProperty("--sidebar-accent-foreground", vars.sidebarAccentForeground)
  }

  const setTheme = (newTheme: "light" | "dark") => {
    setThemeState(newTheme)
    localStorage.setItem("theme", newTheme)
    applyTheme(newTheme, themeColor)
  }

  const setThemeColor = (color: string) => {
    setThemeColorState(color)
    localStorage.setItem("themeColor", color)
    applyTheme(theme, color)
  }

  const toggleTheme = () => {
    const newTheme = theme === "light" ? "dark" : "light"
    setTheme(newTheme)
  }

  return (
    <ThemeContext.Provider
      value={{
        theme,
        themeColor,
        setTheme,
        setThemeColor,
        toggleTheme,
      }}
    >
      {children}
    </ThemeContext.Provider>
  )
}
