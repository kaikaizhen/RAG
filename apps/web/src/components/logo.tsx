import Image from "next/image"
import { cn } from "@/lib/utils"

/**
 * RAGence logo。
 * - 靜態：public/logo-mark.png（黑色線稿 + 透明背景，由原圖的亮度轉成透明度），深色模式反相成白線。
 * - animated：public/logo-animated.gif（呼吸、眨眼、星星閃爍；純白底，所以淺色用 multiply、深色反相後用 screen 讓底色消失）。
 *   使用者設定「減少動態效果」時改顯示靜態版。App 圖示 / favicon 仍是白底 PNG（作業系統需要不透明圖示）。
 */
export function Logo({
  size = 32,
  className,
  priority = false,
  animated = false,
  animatedSrc = "/logo-animated.gif",
}: {
  size?: number
  className?: string
  priority?: boolean
  animated?: boolean
  /** 大尺寸版本（首屏主視覺）可改用 /logo-animated-large.gif，避免放大後模糊 */
  animatedSrc?: string
}) {
  const still = (
    <Image
      src="/logo-mark.png"
      alt="RAGence"
      width={size}
      height={size}
      priority={priority}
      className={cn("select-none dark:invert", animated && "hidden motion-reduce:block", className)}
    />
  )
  if (!animated) return still

  return (
    <>
      <Image
        src={animatedSrc}
        alt="RAGence"
        width={size}
        height={size}
        priority={priority}
        unoptimized // 避免被轉成不會動的靜態圖
        className={cn("select-none mix-blend-multiply motion-reduce:hidden dark:invert dark:mix-blend-screen", className)}
      />
      {still}
    </>
  )
}
