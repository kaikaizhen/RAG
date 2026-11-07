// 只供產生官網截圖用的假後端（範例資料）。用法見 shoot.js 開頭說明。
// 只供截圖用的假後端：範例資料，不連任何真實系統
const http = require("http")
const now = Date.now()
const iso = (daysAgo) => new Date(now - daysAgo * 86400000).toISOString()

const types = [
  { id: 1, nameZh: "人資行政", nameEn: "HR & Admin", nameOther: "" },
  { id: 2, nameZh: "資訊服務", nameEn: "IT Service", nameOther: "" },
  { id: 3, nameZh: "財務報帳", nameEn: "Finance", nameOther: "" },
]
const items = [
  { id: 11, qaTypeID: 1, nameZh: "請假與出勤", nameEn: "Leave", nameOther: "", isActive: true },
  { id: 12, qaTypeID: 1, nameZh: "新人報到", nameEn: "Onboarding", nameOther: "", isActive: true },
  { id: 21, qaTypeID: 2, nameZh: "VPN 與遠端", nameEn: "VPN", nameOther: "", isActive: true },
  { id: 22, qaTypeID: 2, nameZh: "帳號與權限", nameEn: "Accounts", nameOther: "", isActive: true },
  { id: 31, qaTypeID: 3, nameZh: "差旅與報帳", nameEn: "Expenses", nameOther: "", isActive: true },
]
const q = (id, item, question, answer, status, d) => ({ id, qaItemTypeID: item, userID: 1, question, answer, createAt: iso(d), updateAt: null, status })
const questions = [
  q(101, 31, "出差報帳需要準備哪些文件？", "出差報帳需要：①出差申請單（主管已核准）、②交通與住宿的電子發票或收據、③行程說明。請在返回後 14 天內於財務系統送出，逾期需主管補簽。", "Verified", 2),
  q(102, 21, "在家要怎麼連上公司 VPN？", "安裝公司提供的 VPN 用戶端，使用員工帳號登入並輸入手機上的驗證碼即可。首次使用請先向資訊部申請 VPN 權限，約一個工作天開通。", "Verified", 3),
  q(103, 11, "特休假可以分次請嗎？", "可以，最小單位為半天。請在請假系統送出申請並由直屬主管核准；年度結束前未休完的特休，依公司規定折算工資。", "Verified", 5),
  q(104, 12, "新人第一天要帶什麼資料？", "請攜帶身分證明文件、銀行存摺封面與兩吋照片各一張。電腦、帳號與門禁卡會在報到當天由行政與資訊同仁協助設定。", "Verified", 6),
  q(105, 22, "忘記密碼要怎麼重設？", "請至內部入口網站點選「忘記密碼」，以公司信箱接收驗證信後重設。若信箱也無法使用，請聯絡資訊服務台。", "Stale", 9),
  q(106, 31, "交通費的補助上限是多少？", "依舊版辦法為每日 500 元，新版辦法已調整，內容尚待確認。", "Stale", 12),
  q(107, 11, "病假需要附診斷證明嗎？", "連續請假達三天以上需附診斷證明；一至兩天可事後補單說明。", "Inferred", 15),
]
const chats = [
  { id: "3", name: "出差報帳需要準備哪些文件？" },
  { id: "2", name: "在家要怎麼連上公司 VPN？" },
  { id: "1", name: "特休假可以分次請嗎？" },
]
const logs = {
  "3": [{ questions: "出差報帳需要準備哪些文件？", answer: questions[0].answer, createAt: iso(0) }],
}
const send = (res, status, body, extra = {}) => {
  res.writeHead(status, { "Content-Type": "application/json", ...extra })
  res.end(JSON.stringify(body))
}
const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "Authorization, Content-Type, X-Space-Id",
  "Access-Control-Allow-Methods": "GET,POST,PUT,DELETE,OPTIONS",
}
http
  .createServer((req, res) => {
    Object.entries(cors).forEach(([k, v]) => res.setHeader(k, v))
    if (req.method === "OPTIONS") return res.writeHead(204).end()
    const path = req.url.split("?")[0].replace("/api/v1", "")
    if (path === "/Auth/Login") return send(res, 200, { userID: 1, name: "林小明", accessToken: "demo", refreshToken: "demo", manage: "" })
    if (path === "/Space") return send(res, 200, [{ spaceID: 1, key: "u-1", name: "林小明 的空間", role: "owner", isDefault: true }])
    if (path === "/Space/current") return send(res, 200, { spaceID: 1, key: "u-1", name: "林小明 的空間", role: "owner", permissions: ["space.read", "space.manage", "knowledge.read", "knowledge.write"] })
    if (path === "/QaType") return send(res, 200, types)
    if (path === "/QaTypeItem") return send(res, 200, items)
    if (path === "/Question") return send(res, 200, questions)
    if (path === "/Chat" && req.method === "GET") return send(res, 200, chats)
    if (path.startsWith("/Chat/") && path !== "/Chat/ChatStream") return send(res, 200, logs[path.split("/")[2]] || [])
    if (path === "/Chat/ChatStream") {
      let body = ""
      req.on("data", (c) => (body += c))
      return req.on("end", () => {
        if (body.includes("股票")) return send(res, 400, { error: "找不到足夠可靠的相關內容" })
        res.writeHead(200, { "Content-Type": "text/event-stream" })
        const answer = questions[1].answer
        res.write("event: chat\ndata: 9\n\n")
        let i = 0
        const t = setInterval(() => {
          if (i >= answer.length) { clearInterval(t); return res.end() }
          res.write(`data: ${answer[i++]}\n\n`)
        }, 20)
      })
    }
    send(res, 404, { error: "not found" })
  })
  .listen(4010, () => console.log("mock on 4010"))
