/** 相對時間（剛才 / N 小時前 / 昨天 / N 天前 / 日期），給對話清單用 */
export function formatRelativeTime(dateString?: string): string {
  if (!dateString) return ""

  const date = new Date(dateString)
  const diffInMs = Date.now() - date.getTime()
  const diffInHours = Math.floor(diffInMs / (1000 * 60 * 60))
  const diffInDays = Math.floor(diffInMs / (1000 * 60 * 60 * 24))

  if (diffInHours < 1) return "剛才"
  if (diffInHours < 24) return `${diffInHours} 小時前`
  if (diffInDays === 1) return "昨天"
  if (diffInDays < 7) return `${diffInDays} 天前`
  return date.toLocaleDateString("zh-TW")
}
