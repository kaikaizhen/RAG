# RAG 應用平台

一套以 **.NET 8 Web API** 搭配 **Python FastAPI 向量服務** 組成的檢索增強生成（Retrieval-Augmented Generation）問答平台。

使用者可在後台維護問答知識庫（問題分類 / 問題項目 / 問答內容），系統會將問題轉為向量寫入 **Qdrant** 向量資料庫；前台提問時，透過語意相似度檢索出最相關的內容並回覆，同時保留完整的對話歷史紀錄。

---

## 目錄

- [系統架構](#系統架構)
- [專案結構](#專案結構)
- [技術棧](#技術棧)
- [環境需求](#環境需求)
- [快速開始](#快速開始)
  - [1. RAG_Server（向量服務）](#1-rag_server向量服務)
  - [2. RAG（.NET API）](#2-rag net-api)
- [Docker 部署](#docker-部署)
- [設定說明](#設定說明)
- [API 一覽](#api-一覽)
- [安全性注意事項](#安全性注意事項)

---

## 系統架構

```
                 ┌─────────────────────────┐
   使用者  ──►   │   RAG (.NET 8 Web API)   │  ◄── MySQL
                 │  驗證 / 知識庫 / 對話紀錄  │
                 └───────────┬─────────────┘
                             │  HTTP (JSON)
                             ▼
                 ┌─────────────────────────┐
                 │ RAG_Server (FastAPI)     │
                 │  向量化 / 語意檢索        │
                 └───────────┬─────────────┘
                             │
                             ▼
                 ┌─────────────────────────┐
                 │   Qdrant 向量資料庫       │
                 └─────────────────────────┘
```

**運作流程**

1. 於 `.NET API` 建立問答資料 → 呼叫 `RAG_Server` 的 `/questionsToVector` 轉為向量並寫入 Qdrant。
2. 使用者提問 → `.NET API` 呼叫 `/ask` → `RAG_Server` 以語意相似度檢索。
3. 相似度 > 0.7 時直接回傳最相符的答案（`direct_match`）；否則彙整前幾筆內容做摘要回覆（`context_summary`）。
4. `.NET API` 將問答結果寫入 `ChatHeader` / `ChatLog` 保存對話歷史。

---

## 專案結構

```
.
├── RAG/                        # .NET 8 Web API
│   ├── RAG/
│   │   ├── Controllers/        # API 進入點
│   │   ├── Services/           # 商業邏輯
│   │   ├── Repositories/       # 資料存取（EF Core）
│   │   ├── Models/
│   │   │   ├── Entities/       # 資料庫實體
│   │   │   ├── Params/         # 請求參數
│   │   │   ├── Dtos/           # 服務層傳輸物件
│   │   │   └── ViewModels/     # 回應模型
│   │   ├── Extensions/         # DI 註冊與 AutoMapper Profile
│   │   └── Commons/            # 列舉、共用選項
│   ├── Dockerfile
│   └── docker-compose.yml
│
├── apps/web/                   # Next.js 前端（使用 packages/rag-client-sdk）
├── packages/rag-client-sdk/    # 共用 TypeScript client SDK
│
└── RAG_Server/                 # Python FastAPI 向量服務
    ├── main.py
    ├── requirements.txt
    └── Dockerfile
```

分層採 `Controller → Service → Repository`，並以 `Param → Dto → Entity → Dto → ViewModel` 的映射流程（AutoMapper）隔離各層模型。

---

## 技術棧

| 分類 | 技術 |
| --- | --- |
| 後端 API | .NET 8、ASP.NET Core Web API、Swagger (Swashbuckle) |
| ORM | Entity Framework Core 9、Pomelo MySQL Provider |
| 物件映射 | AutoMapper 14 |
| 驗證 | JWT（Access Token / Refresh Token） |
| 向量服務 | Python 3.11、FastAPI、Uvicorn、Pydantic v2 |
| 向量資料庫 | Qdrant（1024 維、Cosine 距離） |
| Embedding | 遠端 Ollama `qwen3-embedding:0.6b`（`/api/embed`，1024 維）；本服務不載入本地模型 |
| 容器化 | Docker、Docker Compose |

---

## 環境需求

- .NET SDK 8.0 以上
- Python 3.11 以上
- MySQL 8.0 以上
- Qdrant（可用 Docker 啟動）
- Docker / Docker Compose（若採容器部署）

> **注意**：`RAG.csproj` 參考了內部共用套件 `Library`（提供 JWT、HTTP 例外、Swagger 等擴充）。此套件託管於私有 BaGet NuGet 來源，非公開發佈。若無該來源存取權，需自行提供對應實作或移除相關參考。

---

## 快速開始

### 1. RAG_Server（向量服務）

```bash
cd RAG_Server

# 建立虛擬環境
python -m venv .venv
source .venv/bin/activate        # Windows: .venv\Scripts\activate

# 安裝套件
pip install -r requirements.txt

# 設定環境變數
cp .env.example .env             # 再依實際環境修改內容

# 啟動 Qdrant（若尚未啟動）
docker run -d -p 6333:6333 --name qdrant qdrant/qdrant

# 啟動服務
uvicorn main:app --host 0.0.0.0 --port 8007
```

Embedding 由遠端 Ollama 提供（`OLLAMA_BASE_URL`），啟動時不載入任何模型。
可透過 `GET /model_status` 檢查 Ollama 狀態，`status: ok` 表示可連線且已安裝指定模型；Ollama 失敗時寫入與查詢回 503，不 fallback。

### 2. RAG（.NET API）

```bash
cd RAG

# 建立本機開發設定
cp RAG/appsettings.Development.example.json RAG/appsettings.Development.json
#   → 填入 MySQL 連線字串與 RagOption:BaseUrl

# 建立 JWT 金鑰設定
cp RAG/encryption_config.example.json RAG/encryption_config.json
#   → 產生金鑰：openssl rand -base64 32

# 若需私有 NuGet 來源
export NUGET_PRIVATE_FEED=http://<your-baget-host>:5555/v3/index.json

dotnet restore
dotnet run --project RAG/RAG.csproj
```

預設位址：`http://localhost:5115`，Swagger 文件於 `http://localhost:5115/swagger`。

---

## 自行註冊與 Gmail 驗證信

預設**關閉**。開啟後任何人都能以信箱註冊：

1. `POST /api/v1/Auth/Register` 收到信箱與密碼 → 寄出驗證信（不論信箱是否已註冊，回應都相同，避免被用來探測帳號）。
2. 使用者點信中連結 → 前端 `/verify-email?token=…` 呼叫 `POST /api/v1/Auth/VerifyEmail`。
3. **此時才建立帳號**，並建立專屬的個人 Space（`u-<使用者編號>`，角色 owner、預設）。每個人的知識庫與對話都只存在自己的 Space，不會互相污染。

設計重點：註冊資料（含密碼雜湊）以 AES-GCM 加密放在驗證連結內，連結 24 小時有效；在點擊之前資料庫裡不存在該帳號，
所以不會有人搶先用別人的信箱註冊並佔住帳號。密碼一律以 PBKDF2-SHA512（210,000 次）儲存；既有的明文密碼在下次登入成功時自動升級。
`POST /api/v1/User` 仍需登入，不是公開端點。

**設定（環境變數，見 `RAG/.env.example`）**

| 變數 | 說明 |
| --- | --- |
| `REGISTRATION_ENABLED` | `true` 才開放；預設 `false`。啟用時缺少下列任何設定，API 會直接啟動失敗 |
| `REGISTRATION_VERIFY_URL_BASE` | 前端驗證頁的完整網址，例如 `https://<前端網域>/verify-email` |
| `MAIL_HOST` / `MAIL_PORT` / `MAIL_USE_SSL` | Gmail：`smtp.gmail.com` / `587` / `true` |
| `MAIL_ACCOUNT` / `MAIL_PASSWORD` | Gmail 帳號與**應用程式密碼** |

Gmail 需先開啟兩步驟驗證，再到 Google 帳戶的「應用程式密碼」建立一組 16 碼密碼（不是登入密碼）。
這組密碼只放在伺服器的 `.env` / 環境變數（`Library__Mail__Password`），**不要**寫進程式碼、設定檔範本或對話。

**上線順序**：① 備份 `UserCache` 後執行 `RAG/RAG/Migrations/Scripts/20261107-email-registration-columns.sql`（放寬 `Account`、`Password` 長度）
→ ② 部署新版 API → ③ 設定上述變數並把 `REGISTRATION_ENABLED` 設為 `true`。順序反了，新版寫入長值時舊欄位會被資料庫拒絕。

**防濫用**：註冊端點全域每分鐘 30 次、驗證端點每個來源 IP 每分鐘 60 次、同一信箱 60 秒內只寄一次。
尚未做：圖形驗證（例如 Cloudflare Turnstile）、每個使用者的知識庫 / 向量用量上限；開放給陌生人之前建議補上。

---

## Docker 部署

```bash
cd RAG
cp .env.example .env        # 填入 NUGET_PRIVATE_FEED、DB_CONNECTION_STRING、RAG_SERVER_BASE_URL
docker compose up -d --build
```

`RAG_Server` 亦可獨立建置：

```bash
cd RAG_Server
docker build -t rag-server .
docker run -d -p 8007:8007 \
  -e QDRANT_HOST=<qdrant-host> \
  -e QDRANT_PORT=6333 \
  --name rag-server rag-server
```

| 服務 | 容器內 Port | 說明 |
| --- | --- | --- |
| RAG (.NET API) | 8006 | 主要 API |
| RAG_Server (FastAPI) | 8007 | 向量化與檢索 |
| Qdrant | 6333 | 向量資料庫 |

---

## 設定說明

所有機敏資訊皆透過**環境變數**或**未納入版控的設定檔**提供，儲存庫中僅保留 `*.example` 範本。

### RAG（.NET API）

| 設定鍵 | 環境變數 | 說明 |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | MySQL 連線字串 |
| `RagOption:BaseUrl` | `RagOption__BaseUrl` | RAG_Server 服務基底位址 |
| `RagOption:AskUrl` 等 | `RagOption__AskUrl` | 各端點路徑，預設值已可直接使用 |
| — | `NUGET_PRIVATE_FEED` | 私有 NuGet v3 index（僅建置時需要） |

`encryption_config.json` 內的 `JWT_SECRET_KEY` 為 JWT 簽章金鑰，**務必**自行產生且不得納入版控。

### RAG_Server（FastAPI）

| 環境變數 | 預設值 | 說明 |
| --- | --- | --- |
| `QDRANT_HOST` | `localhost` | Qdrant 主機 |
| `QDRANT_PORT` | `6333` | Qdrant 連接埠 |
| `QDRANT_COLLECTION` | `rag_knowledge_v2` | Collection 名稱（更換 embedding 模型必須使用新 collection） |
| `OLLAMA_BASE_URL` | `http://192.168.0.188:11434` | Ollama 位址 |
| `OLLAMA_EMBED_MODEL` | `qwen3-embedding:0.6b` | Embedding 模型 |
| `EMBEDDING_DIMENSION` | `1024` | 向量維度（與回傳維度不符時拒絕寫入 / 查詢） |
| `DIRECT_MATCH_THRESHOLD` / `DIRECT_MATCH_MARGIN` / `SIMILAR_SCORE_THRESHOLD` | `0.70` / `0.05` / `0.50` | `/ask` 判斷門檻（依 qwen3 校準）；`DIRECT_MATCH_THRESHOLD`、`SIMILAR_SCORE_THRESHOLD` 與 .NET ContextRuntime 共用同一組值 |
| `SUMMARY_MAX_CHARS` | `500` | 摘要模式回覆的最大字數（只在段落 / 程式碼區塊邊界截斷） |
| `CORS_ORIGINS` | `*` | 允許來源，多筆以逗號分隔 |

---

## API 一覽

### RAG（.NET API）

| 方法 | 路徑 | 說明 |
| --- | --- | --- |
| `POST` | `/api/Auth/Login` | 登入並取得 Access / Refresh Token |
| `GET` | `/api/User/{Id}` | 查詢使用者 |
| `POST` | `/api/User` | 建立使用者 |
| `PUT` | `/api/User` | 更新使用者 |
| `DELETE` | `/api/User/{Id}` | 刪除使用者 |
| `GET` `POST` `PUT` | `/api/QaType` | 問答分類查詢 / 新增 / 更新 |
| `DELETE` | `/api/QaType/{Id}` | 刪除問答分類 |
| `GET` `POST` `PUT` | `/api/QaTypeItem` | 分類項目查詢 / 新增 / 更新 |
| `DELETE` | `/api/QaTypeItem/{Id}` | 刪除分類項目 |
| `GET` `POST` `PUT` | `/api/Question` | 問答查詢 / 新增 / 更新（新增與更新會同步向量） |
| `DELETE` | `/api/Question/{Id}` | 刪除問答（同步刪除向量） |
| `POST` | `/api/Chat` | 提問並取得回覆 |
| `POST` | `/api/Chat/ChatStream` | 串流方式提問 |
| `GET` | `/api/Chat` | 取得使用者對話清單 |
| `GET` | `/api/Chat/{chatId}` | 取得指定對話的訊息紀錄 |
| `DELETE` | `/api/Chat/{chatId}` | 刪除對話紀錄 |

完整規格請參考執行後的 Swagger 頁面。

### RAG_Server（FastAPI）

| 方法 | 路徑 | 說明 |
| --- | --- | --- |
| `GET` | `/` | 服務與模型狀態 |
| `GET` | `/model_status` | Ollama embedding 狀態 |
| `POST` | `/questionsToVector` | 批次將問題轉向量並寫入 Qdrant |
| `POST` | `/ask` | 語意檢索並產生回答 |
| `GET` | `/questions` | 列出已建立的問題（預設上限 100 筆） |
| `DELETE` | `/questions/{question_id}` | 刪除指定問題向量 |
| `DELETE` | `/delete_type/{type_value}` | 刪除指定分類的所有向量 |
| `GET` | `/collections` | 列出所有 Collection |

---

## 安全性注意事項

- `appsettings.Development.json`、`.env`、`encryption_config.json` 已列入 `.gitignore`，**請勿**提交至版控。
- 正式環境請將 `CORS_ORIGINS` 由 `*` 改為明確的網域清單。
- `RAG_Server` 目前未內建驗證機制，請勿直接對外開放，建議僅允許 `.NET API` 由內網存取。
- JWT 金鑰請定期輪替，並使用足夠長度的隨機字串。
