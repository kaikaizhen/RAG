import {
  RagApiError,
  RagClient,
  type StoredSession,
  type TokenStore,
} from "@rag/client-sdk"

/**
 * 全站共用的 RAG client。授權一律由伺服器決定，這裡只保存 token 與顯示用資料。
 *
 * Token 放在記憶體與 localStorage，讓 PWA / 瀏覽器重開後仍保持登入（refresh token 滑動續期，
 * 伺服器端有效期見 Jwt:RefreshTokenDays）。登出或 refresh 失敗時會清除。
 * 注意：localStorage 可被頁面內的 XSS 讀取；回答以 Markdown 渲染且不允許原始 HTML，
 * 要更嚴格需改為 httpOnly cookie 的 BFF。
 */
const SESSION_KEY = "rag.session"
const PROFILE_KEY = "rag.profile"
const SPACE_KEY = "rag.space"

/** 後端位址。留空代表與網站同網域（例如 Nginx 把 /api/ 轉給後端），此時使用目前網址的來源 */
export const API_BASE_URL = process.env.NEXT_PUBLIC_RAG_API_URL ?? ""

export interface Profile {
  userId: number
  name: string
  manage: string
}

/** 舊版存在 sessionStorage，升級後第一次讀取時搬過來，避免已登入的人被登出 */
function migrateLegacy(): void {
  try {
    for (const key of [SESSION_KEY, PROFILE_KEY, SPACE_KEY]) {
      const legacy = window.sessionStorage.getItem(key)
      if (legacy !== null && window.localStorage.getItem(key) === null) window.localStorage.setItem(key, legacy)
      window.sessionStorage.removeItem(key)
    }
  } catch {
    /* ignore */
  }
}

function read<T>(key: string): T | null {
  try {
    const raw = window.localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : null
  } catch {
    return null
  }
}

function write(key: string, value: unknown): void {
  try {
    window.localStorage.setItem(key, JSON.stringify(value))
  } catch {
    /* 無痕模式或被封鎖時只保留在記憶體 */
  }
}

function remove(key: string): void {
  try {
    window.localStorage.removeItem(key)
  } catch {
    /* ignore */
  }
}

class BrowserTokenStore implements TokenStore {
  private memory: StoredSession | null = null

  async get(): Promise<StoredSession | null> {
    return (this.memory ??= read<StoredSession>(SESSION_KEY))
  }

  async set(session: StoredSession): Promise<void> {
    this.memory = { ...session }
    write(SESSION_KEY, session)
  }

  async clear(): Promise<void> {
    this.memory = null
    remove(SESSION_KEY)
    remove(PROFILE_KEY)
    remove(SPACE_KEY)
  }
}

let client: RagClient | null = null

/** 只能在瀏覽器端呼叫（useEffect / 事件處理器內） */
export function getClient(): RagClient {
  if (client) return client
  if (typeof window === "undefined") throw new Error("getClient() 只能在瀏覽器端使用")
  migrateLegacy()
  client = new RagClient({ baseUrl: API_BASE_URL || window.location.origin, tokenStore: new BrowserTokenStore() })
  const savedSpace = read<number>(SPACE_KEY)
  if (savedSpace) client.spaces.select(savedSpace)
  client.on("space-changed", ({ spaceId }) => {
    if (spaceId === null) remove(SPACE_KEY)
    else write(SPACE_KEY, spaceId)
  })
  return client
}

export function getProfile(): Profile | null {
  return typeof window === "undefined" ? null : read<Profile>(PROFILE_KEY)
}

export function setProfile(profile: Profile): void {
  write(PROFILE_KEY, profile)
  window.dispatchEvent(new CustomEvent("profileChanged"))
}

/** 登入：存 session 與顯示資料，並選定預設 Space（Space 只是 hint，授權仍由伺服器決定） */
export async function signIn(account: string, password: string): Promise<Profile> {
  const rag = getClient()
  const result = await rag.auth.login(account, password)
  const profile: Profile = {
    userId: result.userID ?? 0,
    name: result.name?.trim() || account,
    manage: result.manage ?? "free",
  }
  setProfile(profile)
  try {
    const spaces = await rag.spaces.list()
    const preferred = spaces.find((s) => s.isDefault) ?? spaces[0]
    rag.spaces.select(preferred?.spaceID ?? null)
  } catch {
    rag.spaces.select(null) // 交由伺服器依預設規則選擇
  }
  return profile
}

export async function signOut(): Promise<void> {
  await getClient().auth.logout()
  window.location.href = "/login"
}

/**
 * 目前 Space 是否可編輯知識庫（Space 角色的 knowledge.write 權限，由伺服器解析）。
 * 自行註冊的使用者是自己 Space 的 owner；被加入他人 Space 的 viewer 則不行。真正的授權仍由伺服器判斷。
 */
export async function canWriteKnowledge(): Promise<boolean> {
  try {
    const current = await getClient().spaces.current()
    return current.permissions?.includes("knowledge.write") ?? false
  } catch {
    return false
  }
}

/** 取得可顯示給使用者的錯誤訊息 */
export function describeError(error: unknown, fallback = "發生錯誤，請稍後再試"): string {
  if (error instanceof RagApiError) {
    switch (error.status) {
      case 401:
        return "帳號或密碼錯誤，或登入已逾時"
      case 403:
        return "沒有權限執行此操作"
      case 404:
        return error.message && error.message !== "Not Found" ? error.message : "找不到資料"
      case 503:
        return "服務暫時無法使用，請稍後再試"
      default:
        // .NET 把向量服務的失敗都轉成這類訊息，對使用者沒有意義
        if (/ API Error$/.test(error.message)) return "問答引擎暫時連不上，通常是伺服器的問題，請稍後再試"
        return error.message || fallback
    }
  }
  if (error instanceof TypeError) return "無法連接到伺服器，請檢查網路連線"
  return error instanceof Error && error.message ? error.message : fallback
}
