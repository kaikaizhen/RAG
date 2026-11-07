import type { KnowledgeStatus } from "@rag/client-sdk"

export const KNOWLEDGE_STATUSES: { value: KnowledgeStatus; label: string; hint: string; className: string }[] = [
  { value: "Verified", label: "已驗證", hint: "可作為直接依據", className: "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300" },
  { value: "Stale", label: "可能過期", hint: "需要重新確認", className: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300" },
  { value: "Inferred", label: "推論", hint: "非原始來源，僅供參考", className: "bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300" },
  { value: "Superseded", label: "已取代", hint: "不參與檢索", className: "bg-muted text-muted-foreground" },
]

export function statusMeta(status: string | undefined) {
  return KNOWLEDGE_STATUSES.find((s) => s.value === status)
}
