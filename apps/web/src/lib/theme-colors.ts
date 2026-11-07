export interface ThemeColor {
  name: string
  value: string
  lightVars: {
    background: string
    foreground: string
    primary: string
    primaryForeground: string
    accent: string
    accentForeground: string
    sidebar: string
    sidebarForeground: string
    sidebarAccent: string
    sidebarAccentForeground: string
  }
  darkVars: {
    background: string
    foreground: string
    primary: string
    primaryForeground: string
    accent: string
    accentForeground: string
    sidebar: string
    sidebarForeground: string
    sidebarAccent: string
    sidebarAccentForeground: string
  }
}

// Arc 風格的主題顏色配置
export const themeColors: ThemeColor[] = [
  {
    name: "Gray",
    value: "gray",
    lightVars: {
      background: "oklch(1 0 0)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.205 0 0)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.97 0 0)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.985 0 0)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.97 0 0)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.145 0 0)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.922 0 0)",
      primaryForeground: "oklch(0.205 0 0)",
      accent: "oklch(0.269 0 0)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.205 0 0)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.269 0 0)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
  {
    name: "Blue",
    value: "blue",
    lightVars: {
      background: "oklch(0.98 0.01 264.376)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.488 0.243 264.376)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.93 0.05 264.376)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.92 0.04 264.376)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.88 0.06 264.376)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.18 0.02 264.376)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.696 0.17 265.754)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.269 0.05 264.376)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.22 0.04 264.376)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.3 0.06 264.376)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
  {
    name: "Purple",
    value: "purple",
    lightVars: {
      background: "oklch(0.98 0.01 303.9)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.577 0.24 303.9)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.95 0.05 303.9)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.92 0.04 303.9)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.88 0.06 303.9)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.18 0.02 303.9)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.72 0.22 303.9)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.3 0.08 303.9)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.24 0.05 303.9)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.32 0.1 303.9)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
  {
    name: "Pink",
    value: "pink",
    lightVars: {
      background: "oklch(0.99 0.01 345.8)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.627 0.265 345.8)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.96 0.05 345.8)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.94 0.04 345.8)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.9 0.06 345.8)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.18 0.02 345.8)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.75 0.24 345.8)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.32 0.08 345.8)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.25 0.06 345.8)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.35 0.1 345.8)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
  {
    name: "Red",
    value: "red",
    lightVars: {
      background: "oklch(0.99 0.01 27.325)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.577 0.245 27.325)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.96 0.04 27.325)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.94 0.03 27.325)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.9 0.05 27.325)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.18 0.015 27.325)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.704 0.191 22.216)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.32 0.08 27.325)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.24 0.04 27.325)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.35 0.09 27.325)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
  {
    name: "Orange",
    value: "orange",
    lightVars: {
      background: "oklch(0.99 0.01 41.116)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.646 0.222 41.116)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.96 0.04 41.116)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.94 0.03 41.116)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.9 0.05 41.116)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.18 0.015 41.116)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.769 0.188 70.08)",
      primaryForeground: "oklch(0.145 0 0)",
      accent: "oklch(0.32 0.08 41.116)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.24 0.04 41.116)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.35 0.09 41.116)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
  {
    name: "Yellow",
    value: "yellow",
    lightVars: {
      background: "oklch(0.99 0.015 84.429)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.7 0.15 84.429)",
      primaryForeground: "oklch(0.145 0 0)",
      accent: "oklch(0.97 0.05 84.429)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.95 0.04 84.429)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.92 0.06 84.429)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.19 0.02 84.429)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.85 0.17 84.429)",
      primaryForeground: "oklch(0.145 0 0)",
      accent: "oklch(0.35 0.08 84.429)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.26 0.05 84.429)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.38 0.1 84.429)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
  {
    name: "Green",
    value: "green",
    lightVars: {
      background: "oklch(0.98 0.01 184.704)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.6 0.118 184.704)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.94 0.04 184.704)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.92 0.03 184.704)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.88 0.05 184.704)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.17 0.015 162.48)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.696 0.17 162.48)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.3 0.08 162.48)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.22 0.04 162.48)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.32 0.09 162.48)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
  {
    name: "Teal",
    value: "teal",
    lightVars: {
      background: "oklch(0.98 0.01 195)",
      foreground: "oklch(0.145 0 0)",
      primary: "oklch(0.5 0.15 195)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.94 0.04 195)",
      accentForeground: "oklch(0.205 0 0)",
      sidebar: "oklch(0.92 0.03 195)",
      sidebarForeground: "oklch(0.145 0 0)",
      sidebarAccent: "oklch(0.88 0.05 195)",
      sidebarAccentForeground: "oklch(0.205 0 0)",
    },
    darkVars: {
      background: "oklch(0.17 0.015 195)",
      foreground: "oklch(0.985 0 0)",
      primary: "oklch(0.65 0.17 195)",
      primaryForeground: "oklch(0.985 0 0)",
      accent: "oklch(0.3 0.08 195)",
      accentForeground: "oklch(0.985 0 0)",
      sidebar: "oklch(0.22 0.04 195)",
      sidebarForeground: "oklch(0.985 0 0)",
      sidebarAccent: "oklch(0.32 0.09 195)",
      sidebarAccentForeground: "oklch(0.985 0 0)",
    },
  },
]
