/**
 * 給使用者自己的 AI（例如 Claude Code）用的 Skill 範本。Skill 放在使用者自己的機器上，平台只負責存放與檢索：
 * AI 想把通用知識存起來時，只能放進暫存區，由使用者在前端審查後才會成為正式知識。
 * 範本不含任何權杖；權杖請放環境變數，不要寫進檔案。
 */
export function skillTemplate(baseUrl: string): string {
  return `---
name: rag-notes
description: 搜尋我的 RAG 知識庫；整理出通用知識或常見問題時，放進暫存區讓我審查（不會直接寫入知識庫）
---

# RAG 知識庫

API：${baseUrl}/api/v1
驗證：\`Authorization: Bearer $RAG_TOKEN\`（RAG_TOKEN 是環境變數，不要把權杖寫進任何檔案、對話或程式碼）
指定空間（選用）：加上標頭 \`X-Space-Id: <編號>\`，不加則用權杖預設的空間

## 搜尋知識庫

需要權限 knowledge.search。只檢索、不生成答案；回傳 evidence（可當依據）與 similar（僅供參考）。

\`\`\`bash
curl -s -X POST "${baseUrl}/api/v1/Knowledge/search" \\
  -H "Authorization: Bearer $RAG_TOKEN" -H "Content-Type: application/json" \\
  -d '{"query":"<問題>"}'
\`\`\`

- status 為 Grounded 才能當依據回答；Insufficient 時不要編造，直接告訴我知識庫裡沒有。
- evidence 裡 status 為 Stale / Inferred 的內容可信度較低，回答時要註明。

## 把知識放進暫存區

需要權限 inbox.write。**只能放進暫存區**，我會在網站上審查，接受後才會寫入知識庫。

\`\`\`bash
curl -s -X POST "${baseUrl}/api/v1/Inbox" \\
  -H "Authorization: Bearer $RAG_TOKEN" -H "Content-Type: application/json" \\
  -d '{"question":"<問題，500 字內>","answer":"<答案，Markdown，2 萬字內>","sourceRef":"<來源的編號或網址（選用）>"}'
\`\`\`

規則：
1. 只整理「通用、可重複使用」的知識或常見問題；不要放密碼、金鑰、個資、公司機密。
2. 問題要寫成別人會怎麼問，答案要自己讀得懂（不要依賴對話上下文）。
3. 放進去之前先搜尋，已經有的就不要重複。
4. 放進暫存區前先給我看內容，我同意再送。
5. 送出後告訴我：已放進暫存區，請到「暫存區」頁面審查。
`
}
