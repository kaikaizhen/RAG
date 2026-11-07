import type { Metadata } from "next"
import { MobileTopBar } from "@/components/mobile-top-bar"

export const metadata: Metadata = { title: "個人資料" }

export default function Layout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <MobileTopBar title="個人資料" />
      {children}
    </>
  )
}
