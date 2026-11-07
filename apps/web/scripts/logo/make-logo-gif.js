/*
 * 產生動態 logo（public/logo-animated.gif）：貓咪輕輕呼吸起伏、定時眨眼、旁邊的小星星閃爍。
 * 以 public/logo-mark.png（透明背景的線稿）為底，用 Chrome 的 Canvas 逐格繪製，再編成 GIF。
 *
 *   npm i --no-save puppeteer-core gifenc pngjs
 *   node scripts/logo/make-logo-gif.js        # 320px → public/logo-animated.gif
 *   LOGO_SIZE=480 LOGO_OUT=public/logo-animated-large.gif node scripts/logo/make-logo-gif.js   # 首屏大圖
 *   DUMP_FRAMES=<目錄> 可另外輸出幾格 PNG 檢查
 *
 * 背景是純白（GIF 只有 1-bit 透明，半透明邊緣會有鋸齒），網站上以 mix-blend-multiply 讓白底融入背景；
 * 深色模式反相後改用 screen。需要本機安裝 Google Chrome。
 */
const fs = require("fs")
const path = require("path")
const puppeteer = require("puppeteer-core")
const { GIFEncoder, applyPalette } = require("gifenc")
const { PNG } = require("pngjs")

const CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
// 一般尺寸 320（網站上最大約 150px）；首屏大張主視覺用 LOGO_SIZE=480 另外輸出 logo-animated-large.gif
const SIZE = Number(process.env.LOGO_SIZE) || 320
const FRAMES = 36
const DELAY_MS = 70 // 36 格 × 70ms ≈ 2.5 秒一圈
const OUT = process.env.LOGO_OUT || path.join(__dirname, "../../public/logo-animated.gif")
const logoB64 = fs.readFileSync(path.join(__dirname, "../../public/logo-mark.png")).toString("base64")

const html = `<body style="margin:0;background:#fff"><canvas id="c" width="${SIZE}" height="${SIZE}"></canvas><script>
const S = ${SIZE}, K = S / 512, FRAMES = ${FRAMES}
const ctx = document.getElementById("c").getContext("2d")
const img = new Image()
// 眼睛中心與半徑（以 512 為基準，已用 logo-mark.png 實測）
const EYES = [[191.5, 213.3], [320, 213.3]], EYE_R = 17

function star(cx, cy, r, alpha) {
  ctx.save(); ctx.globalAlpha = alpha; ctx.fillStyle = "#000"; ctx.beginPath()
  ctx.moveTo(cx, cy - r)
  ctx.quadraticCurveTo(cx, cy, cx + r, cy); ctx.quadraticCurveTo(cx, cy, cx, cy + r)
  ctx.quadraticCurveTo(cx, cy, cx - r, cy); ctx.quadraticCurveTo(cx, cy, cx, cy - r)
  ctx.fill(); ctx.restore()
}

function blinkAmount(i) { // 0 = 睜眼、0.5 = 半閉、1 = 閉眼；一圈眨一次
  return ({ 24: 0.5, 25: 1, 26: 1, 27: 0.5 })[i] || 0
}

window.draw = (i) => {
  const t = i / FRAMES
  ctx.fillStyle = "#fff"; ctx.fillRect(0, 0, S, S)

  // 呼吸：以底部為軸心輕微伸縮並上下起伏
  const wave = Math.sin(t * 2 * Math.PI)
  ctx.save()
  ctx.translate(S / 2, S * 0.93)
  ctx.scale(1 - 0.01 * wave, 1 + 0.016 * wave)
  ctx.translate(-S / 2, -S * 0.93 - 3 * K * (0.5 + 0.5 * wave))
  ctx.drawImage(img, 0, 0, S, S)

  const b = blinkAmount(i)
  if (b > 0) {
    for (const [ex, ey] of EYES) {
      ctx.fillStyle = "#fff"; ctx.beginPath(); ctx.arc(ex * K, ey * K, (EYE_R + 3) * K, 0, 7); ctx.fill()
      ctx.fillStyle = "#000"; ctx.strokeStyle = "#000"; ctx.lineCap = "round"
      if (b < 1) { ctx.beginPath(); ctx.ellipse(ex * K, ey * K + 3 * K, EYE_R * K, EYE_R * 0.45 * K, 0, 0, 7); ctx.fill() }
      else { ctx.lineWidth = 5 * K; ctx.beginPath(); ctx.arc(ex * K, (ey - 5) * K, 11 * K, 0.18 * Math.PI, 0.82 * Math.PI); ctx.stroke() }
    }
  }
  ctx.restore()

  // 星星：每圈閃兩次，三顆錯開相位
  for (const [x, y, r, phase] of [[462, 66, 19, 0], [38, 150, 14, 0.33], [474, 262, 12, 0.66]]) {
    const tw = 0.5 + 0.5 * Math.sin((t * 2 + phase) * 2 * Math.PI)
    star(x * K, y * K, r * K * (0.35 + 0.65 * tw), 0.25 + 0.75 * tw)
  }
  return c.toDataURL("image/png")
}
img.onload = () => { document.title = "ready" }
img.src = "data:image/png;base64,${logoB64}"
</script>`

;(async () => {
  const browser = await puppeteer.launch({ executablePath: CHROME, headless: "new" })
  try {
    const page = await browser.newPage()
    await page.setViewport({ width: SIZE, height: SIZE })
    await page.setContent(html)
    await page.waitForFunction('document.title === "ready"')

    // 16 階灰階調色盤：線稿的邊緣抗鋸齒夠平滑，檔案也小
    const palette = Array.from({ length: 16 }, (_, n) => { const v = Math.round((n * 255) / 15); return [v, v, v] })
    const gif = GIFEncoder()
    for (let i = 0; i < FRAMES; i++) {
      const dataUrl = await page.evaluate((n) => window.draw(n), i)
      if (process.env.DUMP_FRAMES && [0, 9, 25].includes(i)) fs.writeFileSync(path.join(process.env.DUMP_FRAMES, `frame-${i}.png`), Buffer.from(dataUrl.split(",")[1], "base64"))
      const png = PNG.sync.read(Buffer.from(dataUrl.split(",")[1], "base64"))
      const index = applyPalette(new Uint8Array(png.data), palette)
      gif.writeFrame(index, SIZE, SIZE, { palette, delay: DELAY_MS, repeat: i === 0 ? 0 : undefined })
    }
    gif.finish()
    fs.writeFileSync(OUT, gif.bytes())
    console.log(`${OUT}  ${(gif.bytes().length / 1024).toFixed(0)} KB, ${FRAMES} frames`)
  } finally {
    await browser.close()
  }
})()
