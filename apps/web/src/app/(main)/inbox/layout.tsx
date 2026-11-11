import type { Metadata } from "next"
import { MobileTopBar } from "@/components/mobile-top-bar"

export const metadata: Metadata = { title: "暫存區" }

export default function Layout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <MobileTopBar title="暫存區" />
      {children}
    </>
  )
}
