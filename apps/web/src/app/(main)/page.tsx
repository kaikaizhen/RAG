"use client"

import * as React from "react"
import { AssistantMessage } from "@/components/assistant-message"
import { gapHref } from "@/lib/note-sources"
import { Logo } from "@/components/logo"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { SidebarTrigger } from "@/components/ui/sidebar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { ArrowDown, SendHorizontal, SlidersHorizontal, Square } from "lucide-react"
import { RagApiError, type QaType } from "@rag/client-sdk"
import { describeError, getClient, getProfile } from "@/lib/rag"

interface Message {
  role: "user" | "assistant"
  content: string
  error?: boolean
  /** 知識庫裡找不到答案（不是系統錯誤），可以把問題交給筆記 */
  notFound?: boolean
}

/** 不指定種類 = 搜尋目前 Space 的全部知識 */
const ALL_TYPES = "all"

/**
 * 檢索的空間範圍：current = 只找目前的空間；all = 我所有可讀的空間。
 * 這裡只是「選哪一種」，實際涵蓋哪些空間由伺服器依你的 membership 決定。記在這個瀏覽器，下次開啟沿用。
 */
type SpaceScope = "current" | "all"
const SCOPE_KEY = "rag.searchScope"

function readStoredScope(): SpaceScope {
  try {
    return window.localStorage.getItem(SCOPE_KEY) === "all" ? "all" : "current"
  } catch {
    return "current"
  }
}

export default function MainPage() {
  const [userName, setUserName] = React.useState<string>("使用者")
  const [greeting, setGreeting] = React.useState<string>("早安")
  const [timeGreeting, setTimeGreeting] = React.useState<string>("今天早上")
  const [message, setMessage] = React.useState<string>("")
  const [messages, setMessages] = React.useState<Message[]>([])
  const [isStreaming, setIsStreaming] = React.useState<boolean>(false)
  const [currentChatId, setCurrentChatId] = React.useState<string>("")
  const [currentTypeId, setCurrentTypeId] = React.useState<string>(ALL_TYPES)
  const [qaTypes, setQaTypes] = React.useState<QaType[]>([])
  const [spaceScope, setSpaceScopeState] = React.useState<SpaceScope>("current")
  const [hasManySpaces, setHasManySpaces] = React.useState<boolean>(false)
  const [isMobile, setIsMobile] = React.useState<boolean>(false)
  const abortControllerRef = React.useRef<AbortController | null>(null)
  // handleSubmit 內需要最新的 chatId，但不希望每次變動都重建 listener
  const currentChatIdRef = React.useRef<string>("")
  React.useEffect(() => {
    currentChatIdRef.current = currentChatId
  }, [currentChatId])

  // 對話區捲動：只有「本來就在底部」時才跟隨新內容。使用者往上滑去看前面的內容時，
  // 串流輸出不可以再把畫面拉回最底（否則會卡住、看不到上面）。
  const scrollRef = React.useRef<HTMLDivElement>(null)
  const stickToBottomRef = React.useRef(true)
  const [showJumpToLatest, setShowJumpToLatest] = React.useState(false)

  const scrollToBottom = React.useCallback((smooth = false) => {
    const el = scrollRef.current
    if (!el) return
    el.scrollTo({ top: el.scrollHeight, behavior: smooth ? "smooth" : "auto" })
  }, [])

  const handleScroll = React.useCallback(() => {
    const el = scrollRef.current
    if (!el) return
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 80
    stickToBottomRef.current = atBottom
    setShowJumpToLatest(!atBottom)
  }, [])

  const jumpToLatest = React.useCallback(() => {
    stickToBottomRef.current = true
    setShowJumpToLatest(false)
    scrollToBottom(true)
  }, [scrollToBottom])

  React.useEffect(() => {
    if (stickToBottomRef.current) scrollToBottom()
  }, [messages, scrollToBottom])

  React.useEffect(() => {
    // 檢測是否為手機版
    const checkMobile = () => {
      setIsMobile(window.innerWidth < 768)
    }

    checkMobile()
    window.addEventListener('resize', checkMobile)
    return () => window.removeEventListener('resize', checkMobile)
  }, [])

  // 載入問答種類（作為檢索範圍選項）
  const loadQaTypes = React.useCallback(async () => {
    try {
      setQaTypes(await getClient().qaTypes.list())
    } catch (error) {
      console.error("載入問答種類失敗:", error)
    }
  }, [])

  // 載入特定聊天記錄
  const loadChat = React.useCallback(async (chatId: string) => {
    try {
      const logs = await getClient().chat.logs(chatId)
      setCurrentChatId(chatId)
      stickToBottomRef.current = true
      setMessages(
        logs.flatMap((log): Message[] => [
          { role: "user", content: log.questions ?? "" },
          { role: "assistant", content: log.answer ?? "" },
        ]),
      )
    } catch (error) {
      console.error("載入聊天失敗:", error)
      setMessages([{ role: "assistant", content: describeError(error, "載入對話失敗"), error: true }])
    }
  }, [])

  // 開始新對話
  const startNewChat = React.useCallback(() => {
    abortControllerRef.current?.abort()
    setMessages([])
    setCurrentChatId("")
  }, [])

  React.useEffect(() => {
    const profile = getProfile()
    if (profile?.name) {
      setUserName(profile.name)
    }

    // 根據時間設定問候語
    const updateGreeting = () => {
      const hour = new Date().getHours()
      if (hour >= 5 && hour < 12) {
        setGreeting("早安")
        setTimeGreeting("今天早上")
      } else if (hour >= 12 && hour < 18) {
        setGreeting("午安")
        setTimeGreeting("今天下午")
      } else {
        setGreeting("晚安")
        setTimeGreeting("今天晚上")
      }
    }

    updateGreeting()
    // 每分鐘更新一次問候語
    const interval = setInterval(updateGreeting, 60000)

    loadQaTypes()

    // 只有加入多個空間時才需要「所有空間」選項
    setSpaceScopeState(readStoredScope())
    getClient().spaces.list().then((list) => setHasManySpaces(list.length > 1)).catch(() => setHasManySpaces(false))

    // 監聽 sidebar 事件
    const handleStartNewChat = () => {
      startNewChat()
    }

    const handleLoadChat = async (event: Event) => {
      const { chatId } = (event as CustomEvent<{ chatId: string }>).detail
      await loadChat(chatId)
    }

    const handleChatDeleted = (event: Event) => {
      const { chatId } = (event as CustomEvent<{ chatId: string }>).detail
      // 如果刪除的是當前聊天，清空訊息
      if (chatId === currentChatIdRef.current) {
        startNewChat()
      }
    }

    window.addEventListener('startNewChat', handleStartNewChat)
    window.addEventListener('loadChat', handleLoadChat)
    window.addEventListener('chatDeleted', handleChatDeleted)
    // 換 Space 後種類清單不同，需重新載入並回到「全部」
    const offSpace = getClient().on('space-changed', () => {
      setCurrentTypeId(ALL_TYPES)
      loadQaTypes()
    })

    return () => {
      clearInterval(interval)
      offSpace()
      window.removeEventListener('startNewChat', handleStartNewChat)
      window.removeEventListener('loadChat', handleLoadChat)
      window.removeEventListener('chatDeleted', handleChatDeleted)
    }
  }, [loadQaTypes, loadChat, startNewChat])

  const setSpaceScope = React.useCallback((scope: SpaceScope) => {
    setSpaceScopeState(scope)
    try {
      window.localStorage.setItem(SCOPE_KEY, scope)
    } catch {
      /* 無痕模式或被封鎖時只保留在這次的畫面上 */
    }
  }, [])

  const updateLastMessage = (updater: (previous: Message) => Message) => {
    setMessages(prev => {
      if (prev.length === 0) return prev
      const next = [...prev]
      next[next.length - 1] = updater(next[next.length - 1])
      return next
    })
  }

  // 發送訊息並接收串流輸出
  const handleSubmit = React.useCallback(async (e: React.FormEvent) => {
    e.preventDefault()
    if (!message.trim() || isStreaming) return

    const userMessage = message.trim()
    setMessage("")
    stickToBottomRef.current = true // 自己送出訊息時一定要看到最新內容
    setMessages(prev => [...prev, { role: "user", content: userMessage }, { role: "assistant", content: "" }])

    const controller = new AbortController()
    abortControllerRef.current = controller
    setIsStreaming(true)

    try {
      let answer = ""
      const stream = getClient().chat.stream(userMessage, {
        chatId: currentChatIdRef.current || undefined,
        type: currentTypeId === ALL_TYPES ? undefined : currentTypeId,
        scope: hasManySpaces && spaceScope === "all" ? "all" : "current",
        signal: controller.signal,
      })
      for await (const event of stream) {
        if (event.type === "chat") {
          // 新對話第一次收到編號；之後的追問都接在同一個對話上
          setCurrentChatId(event.chatId)
        } else {
          answer += event.text
          updateLastMessage(() => ({ role: "assistant", content: answer }))
        }
      }
      // 通知 sidebar 更新聊天記錄列表
      window.dispatchEvent(new Event('chatHistoryUpdated'))
    } catch (error: unknown) {
      if (error instanceof Error && error.name === 'AbortError') return
      // 找不到可靠內容等 4xx 是預期中的情況，不當成錯誤噴出
      if (error instanceof RagApiError && error.status < 500) console.warn("發送訊息未成功:", error.message)
      else console.error("發送訊息失敗:", error)
      // 找不到可靠的相關內容時，後端回 4xx 並附原因，直接顯示給使用者
      updateLastMessage(() => ({ role: "assistant", content: describeError(error, "抱歉，發生了錯誤。請稍後再試。"), error: true, notFound: error instanceof RagApiError && error.status === 404 }))
    } finally {
      setIsStreaming(false)
      abortControllerRef.current = null
    }
  }, [message, isStreaming, currentTypeId, spaceScope, hasManySpaces])

  const handleKeyDown = React.useCallback((e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    // 輸入法（注音、拼音等）組字或選字時按 Enter 是「確認選字」，不是送出；
    // Safari 在 compositionend 之後的那次 keydown 會回報 keyCode 229，所以兩個條件都要擋
    if (e.nativeEvent.isComposing || e.keyCode === 229) return
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      handleSubmit(e as unknown as React.FormEvent)
    }
  }, [handleSubmit])

  // 文字框隨內容自動長高（最多約 6 行，超過後內部捲動）
  const inputRef = React.useRef<HTMLTextAreaElement>(null)
  React.useEffect(() => {
    const el = inputRef.current
    if (!el) return
    el.style.height = "auto"
    el.style.height = `${Math.min(el.scrollHeight, 160)}px`
  }, [message])

  const currentTypeName =
    currentTypeId === ALL_TYPES
      ? "全部種類"
      : (() => {
          const type = qaTypes.find((t) => String(t.id) === currentTypeId)
          return type ? type.nameZh || type.nameEn || type.nameOther || `種類 ${type.id}` : "全部種類"
        })()

  // 輸入框：左邊圖示選擇檢索範圍、中間文字、右邊發送圖示（回答中變成停止）
  const renderInputArea = React.useMemo(() => (
    <form onSubmit={handleSubmit} className="w-full max-w-3xl">
      <div className="flex items-end gap-1.5 rounded-3xl border bg-background p-2 shadow-sm transition-shadow hover:shadow-md focus-within:shadow-md">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="relative h-10 w-10 shrink-0 rounded-full text-muted-foreground hover:text-foreground"
              disabled={isStreaming}
              aria-label={`檢索範圍：${currentTypeName}`}
              title={`檢索範圍：${currentTypeName}`}
            >
              <SlidersHorizontal className="h-5 w-5" />
              {(currentTypeId !== ALL_TYPES || (hasManySpaces && spaceScope === "all")) && <span className="absolute right-2 top-2 h-2 w-2 rounded-full bg-primary ring-2 ring-background" />}
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent side="top" align="start" className="min-w-48">
            {hasManySpaces && (
              <>
                <DropdownMenuLabel>空間</DropdownMenuLabel>
                <DropdownMenuRadioGroup value={spaceScope} onValueChange={(value) => setSpaceScope(value as SpaceScope)}>
                  <DropdownMenuRadioItem value="current">目前的空間</DropdownMenuRadioItem>
                  <DropdownMenuRadioItem value="all">我所有的空間</DropdownMenuRadioItem>
                </DropdownMenuRadioGroup>
                <DropdownMenuSeparator />
              </>
            )}
            <DropdownMenuLabel>種類</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuRadioGroup value={currentTypeId} onValueChange={setCurrentTypeId}>
              <DropdownMenuRadioItem value={ALL_TYPES}>全部種類</DropdownMenuRadioItem>
              {qaTypes.map((type) => (
                <DropdownMenuRadioItem key={type.id} value={String(type.id)}>
                  {type.nameZh || type.nameEn || type.nameOther || `種類 ${type.id}`}
                </DropdownMenuRadioItem>
              ))}
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>

        <Textarea
          ref={inputRef}
          rows={1}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={isMobile ? "向 RAGence 提問" : "想了解什麼？直接問我吧"}
          readOnly={isStreaming}
          className="max-h-40 min-h-10 flex-1 resize-none border-0 bg-transparent px-2 py-2.5 text-base shadow-none focus-visible:ring-0 focus-visible:ring-offset-0 placeholder:text-muted-foreground/60"
        />

        {isStreaming ? (
          <Button
            type="button"
            size="icon"
            className="h-10 w-10 shrink-0 rounded-full"
            onClick={() => abortControllerRef.current?.abort()}
            aria-label="停止回答"
            title="停止回答"
          >
            <Square className="h-4 w-4 fill-current" />
          </Button>
        ) : (
          <Button
            type="submit"
            size="icon"
            className="h-10 w-10 shrink-0 rounded-full"
            disabled={!message.trim()}
            aria-label="發送"
            title="發送（Enter）"
          >
            <SendHorizontal className="h-5 w-5" />
          </Button>
        )}
      </div>

      {/* 提示文字 - 只在桌面版顯示 */}
      <p className="mt-3 hidden text-center text-sm text-muted-foreground md:block">
        按 Enter 發送訊息，Shift + Enter 換行
      </p>
    </form>
  ), [message, isStreaming, isMobile, handleSubmit, handleKeyDown, currentTypeId, currentTypeName, qaTypes, hasManySpaces, spaceScope, setSpaceScope])

  return (
    <div className="flex h-dvh w-full flex-col">
      {/* 手機版頂部導航列 */}
      <header className="sticky top-0 z-10 flex h-14 items-center justify-between border-b bg-background px-4 md:hidden">
        <SidebarTrigger />
        <div className="flex items-center gap-2">
          <Logo size={28} />
          <span className="font-semibold">RAGence</span>
        </div>
        <Button variant="ghost" size="sm" onClick={startNewChat}>
          新對話
        </Button>
      </header>

      {messages.length === 0 ? (
        /* 空狀態 - 輸入框在中間 */
        <div className="flex flex-1 flex-col items-center justify-between p-4 md:justify-center">
          {/* 問候語和 Logo */}
          <div className="flex w-full max-w-3xl flex-1 flex-col items-center justify-center space-y-6 md:flex-initial md:space-y-8">
            {/* Logo - 只在手機版顯示 */}
            <div className="flex flex-col items-center gap-4 md:hidden">
              <Logo size={120} priority animated />
              <p className="text-center text-lg text-muted-foreground">
                {timeGreeting}想了解什麼？
              </p>
            </div>

            {/* 桌面版問候語 - 只在桌面版顯示 */}
            <div className="hidden flex-col items-center gap-4 text-center md:flex pb-4">
              <Logo size={128} priority animated />
              <h1 className="text-4xl font-normal tracking-tight text-foreground/90">
                {greeting}，{userName}
              </h1>
            </div>
          </div>

          {/* 輸入框 */}
          {renderInputArea}
        </div>
      ) : (
        /* 有對話 - 輸入框在底部 */
        <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
          {/* 對話區域：原生捲動（min-h-0 讓 flex 子元素可以縮小，內容超出時才會出現捲軸） */}
          <div className="relative min-h-0 flex-1">
          <div ref={scrollRef} onScroll={handleScroll} className="h-full overflow-y-auto p-4">
            {/* role="log"：新訊息會被螢幕閱讀器念出來 */}
            <div className="mx-auto max-w-3xl space-y-6 pb-4" role="log" aria-live="polite" aria-label="對話內容">
              {messages.map((msg, index) => (
                <div
                  key={index}
                  className={`flex items-start gap-2 ${msg.role === "user" ? "justify-end" : "justify-start"}`}
                >
                  {msg.role === "assistant" && (
                    // 底色放在外層：Logo 在深色模式會反相，底色不能一起被反相
                    <span className="mt-1 flex h-8 w-8 shrink-0 items-center justify-center overflow-hidden rounded-full border bg-background">
                      <Logo size={26} />
                    </span>
                  )}
                  <div
                    className={`max-w-[85%] rounded-2xl px-4 py-3 ${
                      msg.role === "user"
                        ? "bg-primary text-primary-foreground"
                        : msg.error
                          ? "border border-destructive/30 bg-destructive/10 text-destructive"
                          : "bg-muted"
                    }`}
                  >
                    {msg.role === "assistant" && !msg.error ? (
                      <AssistantMessage content={msg.content} streaming={isStreaming && index === messages.length - 1} />
                    ) : (
                      <p className="whitespace-pre-wrap text-sm md:text-base">{msg.content}</p>
                    )}
                    {msg.notFound && gapHref(messages[index - 1]?.content ?? "") && (
                      <a
                        href={gapHref(messages[index - 1]?.content ?? "")}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="mt-2 inline-flex min-h-8 items-center gap-1 rounded-full border border-current/30 px-3 text-xs font-medium transition-colors hover:bg-background/60"
                      >
                        交給筆記，下次就答得出來
                      </a>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
          {showJumpToLatest && (
            <Button
              type="button"
              size="icon"
              variant="secondary"
              onClick={jumpToLatest}
              className="absolute bottom-3 left-1/2 h-9 w-9 -translate-x-1/2 rounded-full border shadow-md"
              aria-label="回到最新訊息"
              title="回到最新訊息"
            >
              <ArrowDown className="h-4 w-4" />
            </Button>
          )}
          </div>

          {/* 輸入框固定在底部 */}
          <div className="border-t bg-background p-4">
            <div className="mx-auto w-full max-w-3xl">
              {renderInputArea}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
