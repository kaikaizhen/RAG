"use client"

import { usePathname } from "next/navigation"
import { AppSidebar } from "@/components/app-sidebar"
import { AdminSidebar } from "@/components/admin-sidebar"

export function DynamicSidebar() {
  const pathname = usePathname()
  
  // 如果在 admin-settings 路徑下，使用 AdminSidebar
  const isAdminPage = pathname.startsWith('/admin-settings')
  
  return isAdminPage ? <AdminSidebar /> : <AppSidebar />
}
