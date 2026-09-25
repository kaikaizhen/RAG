# @rag/client-sdk

RAG API 的共用 TypeScript client，供 CLI、VS Code Extension、Web 共用。只呼叫 .NET API 的 `/api/v1/*`；
所有授權（登入、Space membership、資料範圍）一律由伺服器決定。

## 使用

```ts
import { RagClient, RagForbiddenError, RagUnauthorizedError } from '@rag/client-sdk';

const client = new RagClient({ baseUrl: 'https://rag.example.com', tokenStore: myTokenStore });

await client.auth.login(account, password);
const spaces = await client.spaces.list();
client.spaces.select(spaces[0].spaceID ?? null); // 只是 selection hint，不代表已授權
const current = await client.spaces.current();   // 伺服器解析結果；未授權時拋出 RagForbiddenError
const skills = await client.skills.effective();  // 有效 Skill / Workflow metadata（不含內容）

client.on('session-expired', () => { /* 要求重新登入 */ });
client.on('space-changed', () => { /* 清除畫面上與 Space 相關的狀態 */ });
```

- **Token 儲存**：預設 `MemoryTokenStore`。CLI 請以 OS credential store、VS Code Extension 請以 `vscode.SecretStorage` 實作 `TokenStore`；不可使用明文設定檔或瀏覽器 localStorage。
- **401**：自動以 refresh token 換發一次（同時多個請求只會 refresh 一次）；失敗即清除本機 session 並發出 `session-expired`。
- **錯誤類別**：`RagValidationError`(400)、`RagUnauthorizedError`(401)、`RagForbiddenError`(403)、`RagNotFoundError`(404)、`RagConflictError`(409)、`RagServiceUnavailableError`(503)、其他為 `RagApiError`。
- **快取**：Space 清單、目前 Space、有效 Skill 只快取在記憶體，切換 Space、登出、session 過期時全部清除。
- **不提供**：Agent 執行、capability / tool 呼叫、RAG Server 或 Qdrant 直接存取。

## API contract

型別由 `openapi/rag-api.v1.json` 產生（`src/generated/schema.ts`，請勿手改）。API 變更時：

1. 於 `RAG/` 執行 `UPDATE_OPENAPI_SNAPSHOT=1 dotnet test --filter OpenApiContractTests` 更新 snapshot
2. 於本目錄執行 `npm run generate`
3. `npm run check:generated && npm run typecheck && npm test`

.NET 端的 `OpenApiContractTests` 會在 API 與 snapshot 不一致時失敗。

## 測試

```bash
npm test          # 單元與安全測試（e2e 未設定環境變數時略過）
RAG_E2E_BASE_URL=http://localhost:5000 RAG_E2E_ACCOUNT=... RAG_E2E_PASSWORD=... RAG_E2E_FORBIDDEN_SPACE=2 npm run test:e2e
```
