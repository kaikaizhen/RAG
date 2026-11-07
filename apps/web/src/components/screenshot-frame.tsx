import Image from "next/image"
import { cn } from "@/lib/utils"

/** 瀏覽器視窗外框（桌面畫面） */
export function BrowserFrame({ src, alt, className, priority = false }: { src: string; alt: string; className?: string; priority?: boolean }) {
  return (
    <div className={cn("overflow-hidden rounded-xl border bg-background shadow-2xl shadow-black/10 dark:shadow-black/40", className)}>
      <div className="flex items-center gap-1.5 border-b bg-muted/60 px-3 py-2.5" aria-hidden>
        <span className="h-2.5 w-2.5 rounded-full bg-red-400/80" />
        <span className="h-2.5 w-2.5 rounded-full bg-yellow-400/80" />
        <span className="h-2.5 w-2.5 rounded-full bg-green-400/80" />
      </div>
      <Image src={src} alt={alt} width={1440} height={900} priority={priority} sizes="(min-width: 1024px) 800px, 100vw" className="h-auto w-full" />
    </div>
  )
}

/** 手機外框（直式畫面） */
export function PhoneFrame({ src, alt, className }: { src: string; alt: string; className?: string }) {
  return (
    <div className={cn("overflow-hidden rounded-[2.2rem] border-[7px] border-neutral-900 bg-neutral-900 shadow-2xl shadow-black/20 dark:border-neutral-700", className)}>
      <Image src={src} alt={alt} width={780} height={1688} sizes="260px" className="h-auto w-full rounded-[1.6rem]" />
    </div>
  )
}
