import { parseArgs } from 'node:util';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';
import { createPgDatabase } from './db.js';
import { createUsers } from './users.js';
import {
  applyPlan,
  buildPlan,
  createHttpRagClient,
  loadNoteDocs,
  loadNoteDocsViaMcp,
  noteKey,
  notesToolCaller,
  resolveCategories,
  type LoadFilter,
  type NoteDoc,
} from './ragSync.js';

/**
 * 把筆記同步到 RAG 自己的 Space。預設只列出會做什麼，加 --apply 才真的寫入。
 *
 *   npm run sync:rag -- --project 預約系統 --limit 10
 *   npm run sync:rag -- --apply
 *   npm run sync:rag -- --project learning-notes --prune   # 也刪掉 RAG 上其他專案的筆記
 *
 * 筆記從哪裡讀：有 NOTES_MCP_URL 就走筆記服務的 MCP（要 MCP_TOKEN），不用連資料庫；否則用 DATABASE_URL。
 */

const { values } = parseArgs({
  options: {
    apply: { type: 'boolean', default: false },
    project: { type: 'string', multiple: true },
    limit: { type: 'string' },
    prune: { type: 'boolean', default: false },
  },
});

const limit = values.limit === undefined ? undefined : Number(values.limit);
if (limit !== undefined && (!Number.isInteger(limit) || limit < 1)) {
  throw new Error('--limit 必須是正整數');
}
if (values.prune && limit !== undefined) {
  throw new Error('--prune 不能和 --limit 一起用，不然沒列到的筆記會被當成要刪除');
}
// 定時同步用 --apply 執行；缺設定時要失敗，不能默默變成預覽
if (values.apply && !(process.env.RAG_API_URL && process.env.RAG_ACCOUNT && process.env.RAG_PASSWORD)) {
  throw new Error('--apply 需要 RAG_API_URL、RAG_ACCOUNT、RAG_PASSWORD');
}

type Loaded = { docs: NoteDoc[]; complete: boolean };

/**
 * 讀出要同步的筆記，以及判斷刪除用的「所有筆記」key。
 * 有篩選專案時另外讀一次全部，指定專案也能刪掉 RAG 上已不存在的筆記；沒讀完整時不給 key，就不會刪。
 * prune 時只保留這次要同步的筆記，RAG 上其他專案的筆記也會刪掉（例如不想放在別人伺服器上的公司筆記）。
 */
async function loadDocs(filter: LoadFilter, prune: boolean): Promise<{ docs: NoteDoc[]; existingKeys?: Set<string> }> {
  return withLoader(async (load) => {
    const matched = await load(filter);
    if (prune) return { docs: matched.docs, existingKeys: matched.complete ? new Set(matched.docs.map(noteKey)) : undefined };
    const all = filter.projects?.length ? await load({}) : matched;
    if (!all.complete) return { docs: matched.docs };
    return { docs: matched.docs, existingKeys: new Set(all.docs.map(noteKey)) };
  });
}

async function withLoader<T>(run: (load: (filter: LoadFilter) => Promise<Loaded>) => Promise<T>): Promise<T> {
  const { NOTES_MCP_URL, MCP_TOKEN, DATABASE_URL } = process.env;
  if (NOTES_MCP_URL) {
    if (!MCP_TOKEN) throw new Error('有 NOTES_MCP_URL 時也要設定 MCP_TOKEN');
    const client = new Client({ name: 'learning-notes-rag-sync', version: '0.1.0' });
    await client.connect(
      new StreamableHTTPClientTransport(new URL(NOTES_MCP_URL), {
        requestInit: { headers: { Authorization: `Bearer ${MCP_TOKEN}` } },
      }),
    );
    try {
      console.log(`筆記來源：${NOTES_MCP_URL}`);
      const call = notesToolCaller(client);
      return await run((filter) => loadNoteDocsViaMcp(call, filter));
    } finally {
      await client.close();
    }
  }

  if (!DATABASE_URL) throw new Error('請設定 NOTES_MCP_URL 和 MCP_TOKEN，或是 DATABASE_URL');
  const db = createPgDatabase(DATABASE_URL);
  try {
    console.log('筆記來源：資料庫');
    // 直接讀資料庫時只同步原始擁有者的筆記，其他人的筆記不會被送到這個 Space
    const ownerId = await createUsers(db).originalUserId();
    return await run(async (filter) => ({ docs: await loadNoteDocs(db, ownerId, filter), complete: true }));
  } finally {
    await db.close();
  }
}

const { docs: allDocs, existingKeys } = await loadDocs({ projects: values.project }, values.prune);
const docs = limit === undefined ? allDocs : allDocs.slice(0, limit);
console.log(`要同步的筆記：${docs.length} 筆（符合條件共 ${allDocs.length} 筆）`);
if (!existingKeys) {
  console.log('沒有讀到全部筆記（筆記服務可能是不支援翻頁的舊版），這次不會刪除 RAG 上的問答。');
}

const { RAG_API_URL, RAG_ACCOUNT, RAG_PASSWORD, RAG_SPACE_ID } = process.env;
if (!RAG_API_URL || !RAG_ACCOUNT || !RAG_PASSWORD) {
  // 沒設定 RAG 時只預覽會送出的內容，不連線
  console.log('\n沒有設定 RAG_API_URL、RAG_ACCOUNT、RAG_PASSWORD，只預覽前 3 筆會送出的內容：');
  for (const doc of docs.slice(0, 3)) {
    console.log(`\n=== ${doc.kind}#${doc.ref}（${doc.status}）\n[問題]\n${doc.question}\n[答案]\n${doc.answer}`);
  }
} else {
  const client = await createHttpRagClient({
    baseUrl: RAG_API_URL,
    account: RAG_ACCOUNT,
    password: RAG_PASSWORD,
    spaceId: RAG_SPACE_ID ? Number(RAG_SPACE_ID) : undefined,
  });
  console.log(`RAG Space：${client.spaceName}`);

  const itemIds = await resolveCategories(client, values.apply);
  if (!itemIds) {
    console.log('RAG 還沒有「學習筆記」分類，加 --apply 時會先建立，所有筆記都會是新增。');
  } else {
    const remote = await client.listQuestions();
    const plan = buildPlan(docs, remote, itemIds, {
      existingKeys,
    });
    console.log(
      `新增 ${plan.create.length}、更新 ${plan.update.length}、刪除 ${plan.delete.length}、不變 ${plan.unchanged}`,
    );
    for (const item of [...plan.create, ...plan.update].slice(0, 10)) console.log(`  ${item.key} ${item.title}`);
    for (const item of plan.delete.slice(0, 10)) console.log(`  刪除 ${item.key}`);
    if (plan.delete.length > 10) console.log(`  …還有 ${plan.delete.length - 10} 筆要刪除`);

    if (values.apply) {
      await applyPlan(client, plan, (message) => console.log(message));
      console.log('同步完成');
    } else {
      console.log('\n這是預覽，沒有寫入。確認沒問題後加 --apply。');
    }
  }
}
