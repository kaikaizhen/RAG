import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { z } from 'zod';
import { NotesError } from './errors.js';
import { ARTICLE_BODY_MAX, CONFIDENCE, TEST_STATUS, type Notes } from './notes.js';

const NO_SECRETS = '不要放密碼、連線字串、token 或真實個資；含有疑似機密內容的寫入會被拒絕。';

const CONFIDENCE_HELP =
  '內容的來源：general_definition＝一般定義；code_inferred＝從程式碼推測；colleague_confirmed＝已向同事確認（要填 confirmed_by）。' +
  '商業規則只要沒問過人，就是 code_inferred，不可寫成已確認。';

const text = z.string().trim().min(1);
const confidence = z.enum(CONFIDENCE).describe(CONFIDENCE_HELP);
const testStatus = z
  .enum(TEST_STATUS)
  .describe('not_tested＝還沒測；manual＝手動測過；automated＝有自動化測試；production_verified＝已在正式環境確認');
const limit = z.number().int().min(1).max(50).optional().describe('最多回傳幾筆，預設 10');

async function respond(run: () => Promise<unknown>) {
  try {
    const result = await run();
    return { content: [{ type: 'text' as const, text: JSON.stringify(result, null, 2) }] };
  } catch (err) {
    if (err instanceof NotesError) {
      return { isError: true, content: [{ type: 'text' as const, text: err.message }] };
    }
    console.error('MCP 工具執行失敗:', err instanceof Error ? err.message : err);
    return { isError: true, content: [{ type: 'text' as const, text: '伺服器內部錯誤，這筆沒有完成，請稍後再試。' }] };
  }
}

/** 連上伺服器的 Claude 都會收到，換電腦或換人都不用另外設定。 */
export const INSTRUCTIONS = `這是使用者的學習筆記，用來記錄程式改動，並學會程式邏輯和商業邏輯。別人問起時，使用者要能靠筆記回答，所以請用繁體中文和白話書寫。

- 完成程式改動後，主動問使用者要不要用 add_entry 記一筆。使用者同意才寫入。
- 不是程式改動的學習整理（例如把好幾個觀念串起來的說明）用 save_article 記成一般筆記；單一名詞的解釋用 save_concept。
- 「為什麼」要依使用者說過的話填。不知道原因就先問，不要自己編。
- 測試狀態依實際測過的結果填，沒測就是 not_tested。
- 商業規則只要沒問過同事，就標 code_inferred，不確定的地方寫進 open_questions。回答時也要說明可信度，不可把推測說成事實。
- 不要放密碼、連線字串、token 或真實個資。
- 專案或模組被自動建立時，請確認不是打錯名稱。
- 開始工作時可以用 list_knowledge_gaps 看使用者問過但答不出來的問題。做到相關內容時，主動提議把答案記成筆記，記好後用 resolve_knowledge_gap 關閉。`;

export function buildMcpServer(notes: Notes): McpServer {
  const server = new McpServer({ name: 'learning-notes', version: '0.1.0' }, { instructions: INSTRUCTIONS });

  server.registerTool(
    'add_entry',
    {
      title: '新增改動紀錄',
      description:
        '記錄一次程式改動。寫給之後的使用者看，要能靠它回答別人的問題，所以用白話說明，不要只貼程式碼。' +
        '專案或模組不存在時會自動建立，回傳的 project_created／module_created 若為 true 請確認不是打錯名稱。' +
        NO_SECRETS,
      inputSchema: {
        project: text.describe('專案名稱，例如「預約系統」'),
        module: text.optional().describe('專案底下的選單或模組'),
        title: text.describe('一句話標題'),
        what_changed: text.describe('改了什麼'),
        why: text.describe('為什麼要改'),
        logic_notes: text.optional().describe('邏輯判斷：程式怎麼決定、有哪些條件和分支'),
        data_notes: text.optional().describe('資料面：動到哪些資料表、欄位，資料怎麼流動'),
        test_status: testStatus.optional().describe('測試狀態，沒填視為 not_tested。沒實際測過不要填其他值'),
        test_notes: text.optional().describe('測了哪些情境、還有什麼沒測'),
        how_to_explain: text.optional().describe('別人問起時怎麼回答'),
        pr_url: z.string().url().optional().describe('PR 連結'),
        happened_on: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).optional().describe('改動日期 YYYY-MM-DD，預設今天（台北時間）'),
        concept_terms: z.array(text).optional().describe('相關的觀念卡名稱，查不到的會列在 unresolved_concept_terms'),
      },
    },
    (input) => respond(() => notes.addEntry(input)),
  );

  server.registerTool(
    'search_entries',
    {
      title: '搜尋改動紀錄',
      description: '搜尋改動紀錄。query 以空白分隔多個關鍵字，全部都要符合；中文直接比對字串，不需斷詞。不給 query 就列出最新的紀錄。',
      inputSchema: {
        query: z.string().optional().describe('關鍵字'),
        project: text.optional(),
        module: text.optional(),
        test_status: testStatus.optional(),
        limit,
        offset: z.number().int().min(0).optional().describe('略過前幾筆，用來翻頁'),
      },
    },
    (input) => respond(() => notes.searchEntries(input)),
  );

  server.registerTool(
    'update_entry',
    {
      title: '修正改動紀錄',
      description:
        '修正寫錯的改動紀錄內容。改動紀錄是專案的歷史：之後又改了程式要用 add_entry 另記一筆，不要拿這個覆寫。' +
        '使用者同意後才呼叫。沒傳的欄位不變；選填欄位傳空字串會清空，必填欄位不能清空。' +
        NO_SECRETS,
      inputSchema: {
        id: z.number().int().positive().describe('改動紀錄 id，先用 search_entries 查'),
        title: text.optional(),
        what_changed: text.optional(),
        why: text.optional(),
        logic_notes: z.string().optional(),
        data_notes: z.string().optional(),
        test_status: testStatus.optional().describe('沒實際測過不要改成其他值'),
        test_notes: z.string().optional(),
        how_to_explain: z.string().optional(),
        pr_url: z.union([z.string().url(), z.literal('')]).optional().describe('傳空字串會清空'),
        happened_on: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).optional().describe('YYYY-MM-DD'),
      },
    },
    (input) => respond(() => notes.updateEntry(input)),
  );

  server.registerTool(
    'delete_entry',
    {
      title: '刪除改動紀錄',
      description:
        '刪除重複或記錯的改動紀錄，無法復原。呼叫前先用 search_entries 讓使用者看過內容並同意。' +
        '關聯的觀念卡不會被刪，只會解除關聯。',
      inputSchema: {
        id: z.number().int().positive().describe('改動紀錄 id'),
      },
    },
    (input) => respond(() => notes.deleteEntry(input)),
  );

  server.registerTool(
    'get_project_rules',
    {
      title: '查詢專案規則',
      description: '取得專案現行的規則，每條都有可信度。回答時要說明可信度，code_inferred 的內容只能當推測，不可當成事實。',
      inputSchema: {
        project: text,
        module: text.optional().describe('只看這個模組；不填就回傳整個專案'),
      },
    },
    (input) => respond(() => notes.getProjectRules(input)),
  );

  server.registerTool(
    'update_project_rules',
    {
      title: '更新模組規則',
      description:
        '新增或覆寫模組的現行規則，以標題判斷是否同一條；不再適用的規則放進 retire_titles。' +
        '整條覆寫時可信度也要重新判斷：內容改了但沒重新問過同事，就不能沿用已確認。' +
        NO_SECRETS,
      inputSchema: {
        project: text,
        module: text.describe('模組名稱。一個專案有多個系統時寫成「系統名稱／模組」，例如「預約系統／權限」，網站會依系統分組'),
        rules: z
          .array(
            z.object({
              title: text.describe('規則名稱'),
              body: text.describe('規則內容，用白話寫'),
              confidence,
              confirmed_by: text.optional().describe('向誰確認，只有 colleague_confirmed 才填'),
            }),
          )
          .optional(),
        retire_titles: z.array(text).optional().describe('要標記為不再適用的規則名稱'),
      },
    },
    (input) => respond(() => notes.updateProjectRules(input)),
  );

  server.registerTool(
    'save_concept',
    {
      title: '儲存觀念卡',
      description:
        '新增或更新觀念卡，以名稱判斷是否同一張。選填欄位沒傳會保留舊值，傳空字串才清空；可信度每次都要明講。' +
        '不確定的地方寫進 open_questions，不要猜。' +
        NO_SECRETS,
      inputSchema: {
        term: text.describe('觀念名稱，例如「料號」'),
        plain_explanation: text.describe('白話解釋，假設讀者沒有背景知識'),
        confidence,
        confirmed_by: text.optional().describe('向誰確認，只有 colleague_confirmed 才填'),
        why_needed: z.string().optional().describe('為什麼需要這個東西'),
        example: z.string().optional().describe('實際例子，不要用真實個資'),
        where_in_system: z.string().optional().describe('在系統的哪裡看得到'),
        open_questions: z.string().optional().describe('我還不懂、要去問人的地方'),
        related_terms: z.array(text).optional().describe('相關觀念名稱（雙向），查不到的會列在 unresolved_related_terms'),
        link_entry_ids: z.array(z.number().int().positive()).optional().describe('要關聯的改動紀錄 id'),
        project: z
          .string()
          .optional()
          .describe(
            '所屬專案（要是已經有的專案）。觀念卡會跟著這個專案的分享範圍同步到問答；' +
              '沒有所屬專案、也沒連到改動紀錄的觀念卡問答系統問不到。傳空字串取消所屬專案',
          ),
      },
    },
    (input) => respond(() => notes.saveConcept(input)),
  );

  server.registerTool(
    'search_concepts',
    {
      title: '搜尋觀念卡',
      description: '搜尋觀念卡。query 規則同 search_entries。要找待確認的問題時用 has_open_questions=true，或 confidence=code_inferred。',
      inputSchema: {
        query: z.string().optional().describe('關鍵字'),
        confidence: confidence.optional(),
        has_open_questions: z.boolean().optional().describe('true＝只看還有疑問的卡片；false＝只看沒有疑問的'),
        project: z.string().optional().describe('只看屬於這個專案的卡片：指定了所屬專案，或連到這個專案的改動紀錄'),
        limit,
        offset: z.number().int().min(0).optional().describe('略過前幾筆，用來翻頁'),
      },
    },
    (input) => respond(() => notes.searchConcepts(input)),
  );

  server.registerTool(
    'delete_concept',
    {
      title: '刪除觀念卡',
      description:
        '刪除重複或記錯的觀念卡，無法復原。呼叫前先用 search_concepts 讓使用者看過內容並同意。' +
        '關聯的改動紀錄和其他觀念卡不會被刪，只會解除關聯。',
      inputSchema: {
        term: text.describe('觀念名稱，不分大小寫'),
      },
    },
    (input) => respond(() => notes.deleteConcept(input)),
  );

  server.registerTool(
    'save_article',
    {
      title: '儲存一般筆記',
      description:
        '新增或更新一般筆記：不是程式改動的學習整理，例如「ERP 從訂單到出貨怎麼串」。同一個專案裡以標題判斷是否同一篇。' +
        '專案不存在時會自動建立，回傳的 project_created 若為 true 請確認不是打錯名稱。筆記會跟著專案的分享範圍同步到問答。' +
        '可信度每次都要明講；從程式推測的內容標 code_inferred，不確定的地方寫進 open_questions。' +
        NO_SECRETS,
      inputSchema: {
        project: text.describe('專案名稱，例如「global-system」'),
        title: text.describe('標題，寫成別人會問的樣子，例如「ERP 從訂單到出貨怎麼串」'),
        body: text.max(ARTICLE_BODY_MAX).describe('內文，用白話寫；可以用「## 小標」「- 條列」分段。太長就拆成幾篇'),
        confidence,
        confirmed_by: text.optional().describe('向誰確認，只有 colleague_confirmed 才填'),
        module: z.string().optional().describe('專案底下的模組；沒傳保留原本的，傳空字串清空'),
        open_questions: z.string().optional().describe('還不懂、要去問人的地方；沒傳保留原本的，傳空字串清空'),
        concept_terms: z.array(text).optional().describe('相關的觀念卡名稱，有傳就整組換掉；查不到的會列在 unresolved_concept_terms'),
      },
    },
    (input) => respond(() => notes.saveArticle(input)),
  );

  server.registerTool(
    'search_articles',
    {
      title: '搜尋一般筆記',
      description: '搜尋一般筆記。query 規則同 search_entries。不給 query 就列出最近更新的筆記。',
      inputSchema: {
        query: z.string().optional().describe('關鍵字'),
        project: text.optional(),
        module: text.optional(),
        confidence: confidence.optional(),
        has_open_questions: z.boolean().optional().describe('true＝只看還有疑問的；false＝只看沒有疑問的'),
        limit,
        offset: z.number().int().min(0).optional().describe('略過前幾筆，用來翻頁'),
      },
    },
    (input) => respond(() => notes.searchArticles(input)),
  );

  server.registerTool(
    'delete_article',
    {
      title: '刪除一般筆記',
      description:
        '刪除重複或記錯的一般筆記，無法復原。呼叫前先用 search_articles 讓使用者看過內容並同意。' +
        '關聯的觀念卡不會被刪，只會解除關聯。',
      inputSchema: {
        id: z.number().int().positive().describe('筆記 id'),
      },
    },
    (input) => respond(() => notes.deleteArticle(input)),
  );

  server.registerTool(
    'list_knowledge_gaps',
    {
      title: '列出知識缺口',
      description: '列出使用者在問答系統問過、但筆記答不出來的問題。可以依此提議要補哪些筆記。',
      inputSchema: { limit },
    },
    (input) => respond(() => notes.listGaps(input)),
  );

  server.registerTool(
    'resolve_knowledge_gap',
    {
      title: '關閉知識缺口',
      description: '把問題補成筆記（add_entry、save_concept、save_article 或 update_project_rules）後呼叫。還沒寫筆記不要關閉。',
      inputSchema: {
        id: z.number().int().positive().describe('list_knowledge_gaps 回傳的 id'),
        resolution: z.string().optional().describe('答案記在哪裡，例如「觀念卡 BOM」'),
      },
    },
    (input) => respond(() => notes.resolveGap(input)),
  );

  return server;
}
