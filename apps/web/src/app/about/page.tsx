import type { Metadata } from "next"
import Link from "next/link"
import { LibraryBig, LockKeyhole, MessageCircleQuestion, ShieldCheck } from "lucide-react"
import { Logo } from "@/components/logo"
import { Reveal } from "@/components/reveal"
import { BrowserFrame, PhoneFrame } from "@/components/screenshot-frame"
import { Button } from "@/components/ui/button"
import { SITE_DESCRIPTION, SITE_NAME, SITE_TAGLINE, absoluteUrl } from "@/lib/site"
import { BENEFITS, FAQ } from "./content"

const TITLE = `${SITE_NAME}：${SITE_TAGLINE}`
const ICONS = [MessageCircleQuestion, ShieldCheck, LibraryBig, LockKeyhole]

/** 官網展示用的實際畫面（範例資料）。路徑相對於 public/ */
const SHOTS = {
  home: { src: "/screenshots/chat-home.jpg", alt: "RAGence 對話首頁：歡迎畫面與輸入框，左側是最近的對話" },
  chat: { src: "/screenshots/chat-conversation.jpg", alt: "RAGence 對話畫面：詢問出差報帳與 VPN 連線，並得到整理好的答案" },
  notFound: { src: "/screenshots/chat-notfound.jpg", alt: "RAGence 找不到可靠答案時，直接回覆找不到相關內容" },
  manage: { src: "/screenshots/knowledge-manage.jpg", alt: "RAGence 知識管理畫面：新增問答、選擇分類與知識狀態，右側是已儲存的問答" },
  records: { src: "/screenshots/knowledge-records.jpg", alt: "RAGence 問答紀錄：搜尋、編輯與刪除，並以標籤顯示已驗證或可能過期" },
  mobileLight: { src: "/screenshots/mobile-light.jpg", alt: "RAGence 手機版淺色模式的對話畫面" },
  mobileDark: { src: "/screenshots/mobile-dark.jpg", alt: "RAGence 手機版深色模式的對話畫面" },
} as const

export const metadata: Metadata = {
  title: { absolute: TITLE },
  description: SITE_DESCRIPTION,
  alternates: { canonical: "/about" },
  // 整站預設 noindex（私有的 App 頁面）；只有這個公開介紹頁開放收錄
  robots: { index: true, follow: true },
  // 頁面自己設定 openGraph / twitter 時不會繼承根目錄的檔案式分享圖，必須明確指定
  openGraph: {
    title: TITLE,
    description: SITE_DESCRIPTION,
    url: "/about",
    type: "website",
    locale: "zh_TW",
    siteName: SITE_NAME,
    images: [{ url: "/opengraph-image.png", width: 1200, height: 630, alt: `${SITE_NAME} logo` }],
  },
  twitter: { card: "summary_large_image", title: TITLE, description: SITE_DESCRIPTION, images: ["/twitter-image.png"] },
}

/** 結構化資料：只放可以從產品本身驗證的事實（不編造價格、評分、公司資訊或社群連結） */
function jsonLd() {
  const page = absoluteUrl("/about")
  const home = absoluteUrl("/")
  const screenshots = [SHOTS.chat, SHOTS.manage, SHOTS.mobileLight].map((shot) => absoluteUrl(shot.src)).filter(Boolean)
  return [
    {
      "@context": "https://schema.org",
      "@type": "WebSite",
      ...(home && { "@id": `${home}#website`, url: home }),
      name: SITE_NAME,
      description: SITE_DESCRIPTION,
      inLanguage: "zh-TW",
    },
    {
      "@context": "https://schema.org",
      "@type": "SoftwareApplication",
      ...(page && { "@id": `${page}#software`, url: page }),
      name: SITE_NAME,
      description: SITE_DESCRIPTION,
      applicationCategory: "BusinessApplication",
      operatingSystem: "Web（支援加入手機主畫面）",
      inLanguage: "zh-TW",
      featureList: BENEFITS.map((benefit) => benefit.title),
      ...(screenshots.length > 0 && { screenshot: screenshots }),
    },
    {
      "@context": "https://schema.org",
      "@type": "FAQPage",
      ...(page && { "@id": `${page}#faq`, url: page }),
      inLanguage: "zh-TW",
      mainEntity: FAQ.map((item) => ({
        "@type": "Question",
        name: item.question,
        acceptedAnswer: { "@type": "Answer", text: item.answer },
      })),
    },
  ]
}

function Showcase({
  title,
  lead,
  points,
  shot,
  reverse = false,
}: {
  title: string
  lead: string
  points: readonly string[]
  shot: { src: string; alt: string }
  reverse?: boolean
}) {
  return (
    <Reveal>
      <section className={`grid items-center gap-8 lg:gap-12 ${reverse ? "lg:grid-cols-[3fr_2fr]" : "lg:grid-cols-[2fr_3fr]"}`}>
        <div className={reverse ? "lg:order-2" : undefined}>
          <h2 className="text-2xl font-bold tracking-tight md:text-3xl">{title}</h2>
          <p className="mt-3 text-base leading-relaxed text-muted-foreground md:text-lg">{lead}</p>
          <ul className="mt-5 space-y-2.5">
            {points.map((point) => (
              <li key={point} className="flex gap-3 text-sm md:text-base">
                <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-primary" aria-hidden />
                <span>{point}</span>
              </li>
            ))}
          </ul>
        </div>
        <figure className={reverse ? "lg:order-1 lg:-ml-4" : "lg:-mr-4"}>
          <BrowserFrame src={shot.src} alt={shot.alt} />
          <figcaption className="mt-3 text-center text-xs text-muted-foreground">示意畫面（範例資料）</figcaption>
        </figure>
      </section>
    </Reveal>
  )
}

export default function AboutPage() {
  // < 轉義避免內容中出現 </script> 時提早結束標籤
  const serialized = JSON.stringify(jsonLd()).replace(/</g, "\\u003c")

  return (
    <div className="relative min-h-screen overflow-x-hidden bg-background text-foreground">
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: serialized }} />

      {/* 背景裝飾 */}
      <div aria-hidden className="pointer-events-none absolute inset-x-0 top-0 -z-10 h-[640px] bg-[radial-gradient(60%_60%_at_50%_0%,color-mix(in_oklab,var(--primary)_10%,transparent),transparent)]" />

      <header className="sticky top-0 z-20 border-b bg-background/80 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4">
          <Link href="/about" className="flex items-center gap-2 text-lg font-semibold">
            <Logo size={34} priority />
            {SITE_NAME}
          </Link>
          <nav className="flex items-center gap-2" aria-label="帳號">
            <Button asChild variant="ghost" size="sm">
              <Link href="/login">登入</Link>
            </Button>
            <Button asChild size="sm">
              <Link href="/signup">註冊</Link>
            </Button>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-6xl space-y-24 px-4 pb-24 pt-12 md:pt-20">
        {/* 主視覺 */}
        <section>
          <div className="grid items-center gap-8 lg:grid-cols-[1.3fr_1fr] lg:gap-4">
            <div className="text-center lg:text-left">
              <p className="inline-flex items-center gap-2 rounded-full border bg-background px-3 py-1 text-xs text-muted-foreground md:text-sm">
                <Logo size={18} />
                {SITE_TAGLINE}
              </p>
              <h1 className="mt-5 text-4xl font-bold leading-tight tracking-tight md:text-6xl">
                把團隊的知識，
                <br />
                變成<span className="text-primary">隨問隨答</span>的好幫手
              </h1>
              <p className="mx-auto mt-5 max-w-xl text-base leading-relaxed text-muted-foreground md:text-xl lg:mx-0">
                把常見問題整理一次，之後不管是新人還是老手，問一句就能得到有依據的答案；找不到的，它會老實說找不到。
              </p>
            </div>

            {/* 主視覺：大張、斜放的動態 logo，像活動海報 */}
            <div className="relative order-first mx-auto flex w-full max-w-[460px] justify-center lg:order-last lg:ml-6 lg:max-w-none">
              <div aria-hidden className="absolute inset-[8%] rounded-full bg-primary/10 blur-3xl" />
              {/* 注意：GIF 是白底，靠 mix-blend-multiply 融入背景；祖先若有 transform 會形成獨立的堆疊情境、混合失效，
                  所以旋轉與漂浮動畫都直接掛在圖片本身（Tailwind 的 rotate 是獨立屬性，不會和漂浮的 transform 衝突） */}
              <div className="relative">
                <Logo
                  size={440}
                  priority
                  animated
                  animatedSrc="/logo-animated-large.gif"
                  className="h-auto w-[260px] -rotate-[8deg] animate-float-slow transition-[rotate] duration-500 hover:-rotate-2 sm:w-[340px] lg:w-[440px]"
                />
                <span className="absolute -left-2 top-6 rotate-[-6deg] rounded-full border-2 border-foreground bg-background px-3 py-1 text-xs font-semibold shadow-md sm:-left-6 sm:text-sm">
                  問一句，就有答案
                </span>
                <span className="absolute -right-2 bottom-10 rotate-[5deg] rounded-full bg-foreground px-3 py-1 text-xs font-semibold text-background shadow-md sm:-right-4 sm:text-sm">
                  找不到，就老實說
                </span>
              </div>
            </div>
          </div>

          <div className="relative mx-auto mt-12 max-w-4xl">
            {/* 手機寬度改用直式畫面，桌面畫面縮太小會看不清楚 */}
            <BrowserFrame {...SHOTS.home} priority className="hidden text-left md:block" />
            <div className="mx-auto w-56 md:hidden">
              <PhoneFrame {...SHOTS.mobileLight} />
            </div>
            <div className="absolute -bottom-8 -right-2 hidden w-40 animate-float-slow md:block lg:-right-10 lg:w-52">
              <PhoneFrame {...SHOTS.mobileLight} />
            </div>
          </div>
        </section>

        {/* 一眼看懂的好處 */}
        <Reveal>
          <section aria-label="主要好處" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {BENEFITS.map((benefit, index) => {
              const Icon = ICONS[index]
              return (
                <article key={benefit.title} className="rounded-2xl border bg-card p-6 transition-all hover:-translate-y-1 hover:shadow-lg">
                  <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Icon className="h-5 w-5" />
                  </span>
                  <h2 className="mt-4 text-lg font-semibold">{benefit.title}</h2>
                  <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{benefit.body}</p>
                </article>
              )
            })}
          </section>
        </Reveal>

        <Showcase
          title="像跟同事聊天一樣提問"
          lead="想知道什麼，直接問。答案馬上出現，不用再翻資料夾，也不用打擾同事。"
          points={["用平常說話的方式輸入問題", "點輸入框左邊的圖示，就能只在某個分類裡找答案", "對話自動保存，隨時回頭查看"]}
          shot={SHOTS.chat}
        />

        <Showcase
          reverse
          title="找不到，就老實說"
          lead="它不會為了回答而亂編。知識庫裡沒有可靠的答案時，會直接告訴你找不到，讓你知道該去問人，而不是被錯誤資訊誤導。"
          points={["回答只來自你們整理的內容", "不確定的時候不亂猜", "讓團隊放心把它當成第一線窗口"]}
          shot={SHOTS.notFound}
        />

        <Showcase
          title="自己動手整理，馬上生效"
          lead="把常見問題與答案分門別類寫進去，儲存後立刻就能被問到。每筆內容都可以標示可信程度。"
          points={["依分類整理，新增與修改都很簡單", "標示「已驗證」、「可能過期」、「推論」或「已取代」", "已取代的舊內容不會再被拿來回答"]}
          shot={SHOTS.manage}
        />

        <Showcase
          reverse
          title="所有問答，一目了然"
          lead="用搜尋快速找到某一筆，隨手修改或刪除；也可以一次選取多筆整理，知識庫越用越乾淨。"
          points={["搜尋問題或答案內容", "狀態標籤清楚標出需要確認的內容", "可一次選取多筆刪除"]}
          shot={SHOTS.records}
        />

        {/* 手機 */}
        <Reveal>
          <section className="overflow-hidden rounded-3xl border bg-muted/40 px-6 py-14 text-center md:px-12">
            <h2 className="text-2xl font-bold tracking-tight md:text-3xl">手機、電腦都順手</h2>
            <p className="mx-auto mt-3 max-w-xl text-base leading-relaxed text-muted-foreground md:text-lg">
              不用下載，打開瀏覽器就能用；也能加到主畫面，像 App 一樣一點就開。支援淺色與深色模式，白天晚上都舒服。
            </p>
            <div className="mt-10 flex items-end justify-center gap-5 md:gap-10">
              <div className="w-36 md:w-52">
                <PhoneFrame {...SHOTS.mobileLight} />
              </div>
              <div className="w-36 animate-float-slow md:w-52">
                <PhoneFrame {...SHOTS.mobileDark} />
              </div>
            </div>
            <p className="mt-6 text-xs text-muted-foreground">示意畫面（範例資料）</p>
          </section>
        </Reveal>

        {/* 常見問題 */}
        <Reveal>
          <section aria-labelledby="faq" className="mx-auto max-w-3xl space-y-6">
            <h2 id="faq" className="text-center text-2xl font-bold tracking-tight md:text-3xl">常見問題</h2>
            <div className="space-y-3">
              {FAQ.map((item, index) => (
                <details key={item.question} className="group rounded-xl border bg-card p-5 open:shadow-sm" open={index === 0}>
                  <summary className="cursor-pointer list-none font-semibold marker:content-none">
                    <span className="flex items-center justify-between gap-4">
                      {item.question}
                      <span aria-hidden className="text-xl leading-none text-muted-foreground transition-transform group-open:rotate-45">+</span>
                    </span>
                  </summary>
                  <p className="mt-3 text-sm leading-relaxed text-muted-foreground md:text-base">{item.answer}</p>
                </details>
              ))}
            </div>
          </section>
        </Reveal>
      </main>

      <footer className="border-t">
        <div className="mx-auto max-w-6xl px-4 py-6 text-center text-sm text-muted-foreground">
          © {new Date().getFullYear()} {SITE_NAME}
        </div>
      </footer>
    </div>
  )
}
