/**
 * 學習筆記的 logo：和問答系統（RAGence）同一隻貓，RAGence 是抱著書在讀，這裡是拿鉛筆在筆記本上寫。
 * 線條用 currentColor、紙和手的底色用 --logo-paper，深色主題不用另外一張圖。
 * animated 時鉛筆會來回寫、最後一行字一筆一筆寫出來、偶爾眨眼；減少動態效果時不動。
 * 分頁圖示是 app/icon.svg（同一張圖，裁得比較緊、線條加粗）。
 */
export function Logo({ size = 28, animated = false, className }: { size?: number; animated?: boolean; className?: string }) {
  return (
    <svg
      viewBox="46 44 420 420"
      width={size}
      height={size}
      fill="none"
      stroke="currentColor"
      strokeWidth={16}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={['logo', animated && 'is-animated', className].filter(Boolean).join(' ')}
      aria-hidden="true"
    >
      <path d="M112 440 C92 400 88 340 112 300 C90 276 84 236 96 200 C104 160 112 110 124 76 C130 58 146 56 160 66 C180 80 196 92 210 98 C240 90 272 90 302 98 C316 92 332 80 352 66 C366 56 382 58 388 76 C400 110 408 160 416 200 C428 236 422 276 400 300 C424 340 420 400 400 440" />
      <path d="M148 142 C150 124 162 112 180 112 M364 142 C362 124 350 112 332 112" />
      <g className="logo-eyes" fill="currentColor" stroke="none">
        <circle cx="192" cy="212" r="17" />
        <circle cx="320" cy="212" r="17" />
      </g>
      <path d="M250 214 H262 L256 221 Z" fill="currentColor" strokeWidth={8} />
      <path d="M226 226 C230 244 252 244 256 226 C260 244 282 244 286 226" strokeWidth={12} />
      <path d="M118 224 L158 230 M122 252 L160 246 M394 224 L354 230 M390 252 L352 246" strokeWidth={10} />
      <rect x="164" y="304" width="184" height="148" rx="14" fill="var(--logo-paper, #fff)" />
      <path d="M196 290 V316 M226 290 V316 M256 290 V316 M286 290 V316 M316 290 V316" strokeWidth={12} />
      <path d="M194 354 H318 M194 386 H300" strokeWidth={10} />
      <path className="logo-ink" d="M194 418 C208 406 220 428 236 416 S262 406 280 418" strokeWidth={10} pathLength={1} />
      <path d="M152 344 C136 348 134 366 148 372 C134 378 134 398 150 402 C138 410 142 430 164 428" fill="var(--logo-paper, #fff)" />
      <g className="logo-hand">
        <g transform="rotate(40 330 360)">
          <path d="M318 296 H342 V410 L330 436 L318 410 Z" fill="var(--logo-paper, #fff)" />
          <path d="M318 410 H342 M318 316 H342" />
        </g>
        <path
          d="M376 300 C356 296 340 310 348 322 C332 328 334 348 350 350 C338 360 346 378 366 374 C388 370 400 350 398 328 C396 310 390 302 376 300 Z"
          fill="var(--logo-paper, #fff)"
        />
      </g>
    </svg>
  );
}
