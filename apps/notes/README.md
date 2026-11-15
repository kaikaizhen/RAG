# 學習筆記 app

記錄每次程式改了什麼，並累積程式邏輯和商業邏輯的觀念。Claude 透過 MCP 寫入和查詢。原本是獨立的 repo（doraeyi/learning-notes），搬進 RAG 後放在 `apps/notes`，和問答系統部署在同一台主機。

這裡是後端：API、MCP 和資料庫。筆記網站在前端 repo [doraeyi/ragence](https://github.com/doraeyi/ragence) 的 `apps/notes`，部署在 Vercel。寫入都透過 Claude，網頁只能看。

從開新專案、記筆記到用 RAG 問答的完整步驟，見 [docs/使用流程.md](docs/使用流程.md)。

## 架構

```
Claude ─HTTPS + MCP_TOKEN→ Nginx → api（Hono + MCP）→ PostgreSQL
瀏覽器 ─HTTPS + 密碼登入→ 筆記網站（Vercel）─HTTPS + API_TOKEN→ Nginx → api
```

| 路徑 | 說明 |
| --- | --- |
| `POST /mcp` | MCP（無狀態 Streamable HTTP），需要 `Authorization: Bearer <MCP_TOKEN>` |
| `GET /health` | 健康檢查，不需要 token |
| `/api/*` | 筆記網站用的 API，需要 `API_TOKEN` |

網站在 Vercel，所以 `/api` 要對外開放，靠 `API_TOKEN` 保護。拿到這個 token 就能以任何使用者的身分讀筆記，所以它只能放在 Vercel 的環境變數和這台主機的 `.env`，至少 32 字元，外洩時兩邊一起換。

| 檔案 | 內容 |
| --- | --- |
| `migrations/` | 資料表定義，啟動時自動套用還沒跑過的檔案 |
| `src/notes.ts` | 商業邏輯和 SQL，之後的前端 API 也會共用 |
| `src/mcp.ts` | 九個 MCP 工具的定義和說明 |
| `src/secretGuard.ts` | 寫入前擋密碼、連線字串、token、email |
| `src/ragSync.ts` | 把筆記同步到 RAG 自己的 Space（雛形） |

## 本機開發

測試用 PGlite（WASM 版 PostgreSQL），不需要 Docker 或資料庫：

```bash
npm install
```

```bash
npm test
```

```bash
npm run typecheck
```

筆記網站在前端 repo [doraeyi/ragence](https://github.com/doraeyi/ragence) 的 `apps/notes`。本機開發時，把網站的 `API_URL` 指到這裡啟動的 API（預設 `http://localhost:3100`），兩邊的 `API_TOKEN` 設成一樣。

## 部署（和 RAG 同一台主機）

在主機上的 RAG repo 裡，到 `apps/notes` 依 `.env.example` 建立 `.env`，然後建置並啟動。之後要更新，`git pull` 後再執行同一行：

```bash
docker compose up -d --build
```

兩個容器都只綁在本機，由 Nginx 轉發，和 rag-api 的做法相同：

| 容器 | 本機 port | 對外開放 |
| --- | --- | --- |
| notes-api | 8008（`NOTES_API_PORT`） | `/mcp`、`/health`、`/api/` |
| notes-db | 不開 | 不開 |

Nginx 設定範例，網域換成實際的，HTTPS 憑證照主機原本的設定加上去。一定要走 HTTPS，`API_TOKEN` 和 `MCP_TOKEN` 都在標頭裡：

```nginx
server {
    server_name notes-api.doraeyi.com;
    location = /mcp {
        proxy_pass http://127.0.0.1:8008;
        proxy_buffering off;
    }
    location = /health { proxy_pass http://127.0.0.1:8008; }
    location /api/ { proxy_pass http://127.0.0.1:8008; }
    location / { return 404; }
}
```

筆記網站在 Vercel 的設定見前端 repo。網站的 `API_URL` 設成這裡的網址（例如 `https://notes-api.doraeyi.com`），`API_TOKEN` 和這裡的 `.env` 相同。

## 登入和帳號

筆記網站用 RAG 的帳號登入，帳號密碼交給 `RAG_API_URL` 驗證，這裡不存密碼。第一次登入會自動建立筆記帳號，每個人只看得到自己的筆記。

每位使用者在網站的「Token」頁產生自己的 MCP token，資料庫只存雜湊，明文只顯示一次。`.env` 的 `MCP_TOKEN` 是多人化之前的 token，只能碰原始擁有者的筆記。

多人化之前的筆記歸給 `NOTES_OWNER_RAG_USER_ID` 這位 RAG 使用者。**要在這位使用者第一次登入前設定好**，否則會建立一個空的新帳號，舊筆記接不上。

## 分享範圍和背景同步

每個專案在網站的「分享範圍」頁選擇：`只留在筆記`（預設，不離開這台主機）、`我的問答`（同步到自己的 RAG Space）、`公開`（寫進公開 Space，所有登入者問得到）。改回不分享時，內容會從 RAG 刪除。

背景同步在 API 服務裡每分鐘跑一次，需要 `.env` 設定 `SYNC_KEY`（至少 32 字元，用來加密使用者登入時留下的 RAG refresh token）。公開專案另外需要 `RAG_PUBLIC_ACCOUNT`、`RAG_PUBLIC_PASSWORD`、`RAG_PUBLIC_SPACE_ID`；RAG 後端要設定 `RagOption__PublicSpaceId` 為同一個 Space，問答才會查公開知識。

問答答不出來的問題會變成「知識缺口」，Claude 可以用 `list_knowledge_gaps` 看到並提議補成筆記。

## 備份

[scripts/backup.sh](scripts/backup.sh) 由 cron 每天執行，備份存在 `apps/notes/backups/`（不進 git），保留 14 天。crontab 範例，路徑換成主機上 RAG repo 的位置：

```
0 3 * * * /路徑/RAG/apps/notes/scripts/backup.sh >> /路徑/RAG/apps/notes/backups/backup.log 2>&1
```

備份和資料庫在同一台主機，能救誤刪和資料損壞，主機本身壞掉時救不回來，重要時要另外複製到別的地方。

還原前先確認要還原到哪個資料庫。建立空資料庫後匯入：

```bash
docker compose exec -T notes-db createdb -U notes 資料庫名稱
```

```bash
gzip -dc backups/備份檔.sql.gz | docker compose exec -T notes-db psql -q -U notes -d 資料庫名稱 -v ON_ERROR_STOP=1
```

## 同步到 RAG（雛形）

把改動紀錄、觀念卡和現行規則推到 [RAG](https://github.com/kaikaizhen/RAG) 自己的 Space，在 RAG 用語意搜尋問筆記。RAG 只比對「問題」欄位，回答是整篇筆記原文，不會另外生成。

在 `.env` 設定筆記來源和 `RAG_*`（見 `.env.example`）。筆記來源有兩種：

- `NOTES_MCP_URL` 加 `MCP_TOKEN`：透過筆記服務的 MCP 讀，不用連資料庫，在自己電腦就能跑
- `DATABASE_URL`：直接查資料庫，要能連到主機上的 PostgreSQL

預設只預覽，不寫入：

```bash
npm run sync:rag -- --project 預約系統 --limit 10
```

確認沒問題後加 `--apply`：

```bash
npm run sync:rag -- --project 預約系統 --limit 10 --apply
```

- 筆記和 RAG 問答靠答案最後一行的 `來源：learning-notes entry#12` 對應，沒有這行的問答不會被動到。規則用 `rule#專案／模組／標題`，因為 MCP 查規則不回傳 id
- MCP 每次最多回 50 筆又不能翻頁，所以會依專案、測試狀態和可信度拆開查。有任何一次查滿時可能漏讀，這次就不刪除
- 筆記已經刪掉的問答，同步時會從 RAG 刪掉；有 `--project` 時也會（另外讀一次全部筆記來判斷），其他專案的問答保留不動
- RAG 的分類是所有使用者共用的，所以只建通用的「學習筆記／改動紀錄、觀念、規則」，專案名稱只出現在自己 Space 的問答裡
- 筆記會存到 RAG 的伺服器上，公司相關的內容要先想清楚要不要送

## 連接 Claude Code

把網址和 token 換成實際的值。token 只放在本機設定，不要寫進任何 repo：

```bash
claude mcp add --transport http --scope user learning-notes https://你的子網域/mcp --header "Authorization: Bearer 你的token"
```

## 資料規則

- **可信度**：規則和觀念卡都必填，沒有預設值。`general_definition` 一般定義、`code_inferred` 程式碼推測、`colleague_confirmed` 已向同事確認（必須填向誰確認）。
- **機密**：`secretGuard` 只是安全網。姓名、電話、身分證的格式容易和料號混淆，沒有自動偵測，寫入時要自己注意。
- **搜尋**：`ILIKE` 搭配 `pg_trgm` 索引。一兩個字的查詢沒有 trigram 可用，會掃整張表，以筆記的資料量不是問題。
