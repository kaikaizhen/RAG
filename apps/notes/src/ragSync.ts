import type { Client } from '@modelcontextprotocol/sdk/client/index.js';
import type { Queryable } from './db.js';
import { CONCEPT_IN_PROJECT } from './notes.js';

/**
 * 把筆記同步到 RAG（kaikaizhen/RAG）自己的 Space，讓 RAG 可以用語意搜尋回答。
 *
 * RAG 只把「問題」欄位轉成向量，答案原文回傳。使用者的提問通常很短，問題欄位放一大段內容反而比對不到，
 * 所以問題只放標題和範圍，答案放整篇筆記。對照關係靠答案最後一行的來源標記，不另外存狀態；沒有標記的問答一律不動。
 */

export type NoteKind = 'entry' | 'concept' | 'rule' | 'article';

export interface NoteDoc {
  kind: NoteKind;
  /** 改動紀錄和觀念卡用 id；規則用「專案／模組／標題」，因為 MCP 查規則不回傳 id，而標題在模組內不重複。 */
  ref: string;
  title: string;
  question: string;
  answer: string;
  status: 'Verified' | 'Inferred';
}

export interface RemoteQuestion {
  id: number;
  qaItemTypeID: number;
  question: string;
  answer: string;
  status: string;
}

export interface QuestionPayload {
  qaItemTypeID: number;
  question: string;
  answer: string;
  status: string;
}

/** RAG 的分類是所有使用者共用的字典，所以只用通用名稱，專案名稱放在問答內容（只在自己的 Space）。 */
export const RAG_CATEGORY = {
  type: '學習筆記',
  items: { entry: '改動紀錄', concept: '觀念', rule: '規則', article: '筆記' } as Record<NoteKind, string>,
};

export interface RagClient {
  listTypes(): Promise<Array<{ id: number; nameZh: string }>>;
  createType(nameZh: string): Promise<void>;
  listItems(): Promise<Array<{ id: number; qaTypeID: number; nameZh: string }>>;
  createItem(qaTypeID: number, nameZh: string): Promise<void>;
  listQuestions(): Promise<RemoteQuestion[]>;
  createQuestion(payload: QuestionPayload): Promise<void>;
  updateQuestion(payload: QuestionPayload & { id: number }): Promise<void>;
  deleteQuestion(id: number): Promise<void>;
}

const QUESTION_MAX_CHARS = 600;
const MARKER_PREFIX = '來源：learning-notes';
const MARKER_RE = /來源：learning-notes (entry|concept|rule|article)#([^\n]+?)\s*$/;

const TEST_STATUS_LABEL: Record<string, string> = {
  not_tested: '沒測過',
  manual: '手動測過',
  automated: '有自動測試',
  production_verified: '正式環境驗證過',
};

const CONFIDENCE_LABEL: Record<string, string> = {
  general_definition: '一般定義',
  code_inferred: '從程式推測',
  colleague_confirmed: '已向同事確認',
};

export function markerFor(kind: NoteKind, ref: string | number): string {
  return `${MARKER_PREFIX} ${kind}#${ref}`;
}

export function parseMarker(answer: string): { kind: NoteKind; ref: string } | null {
  const match = MARKER_RE.exec(answer);
  return match ? { kind: match[1] as NoteKind, ref: match[2]! } : null;
}

function keyOf(kind: NoteKind, ref: string): string {
  return `${kind}#${ref}`;
}

function truncate(text: string, max: number): string {
  return text.length <= max ? text : `${text.slice(0, max - 1)}…`;
}

function section(heading: string, body: string | null | undefined): string[] {
  return body?.trim() ? [`### ${heading}`, body.trim(), ''] : [];
}

function scopeLabel(project: string, module: string | null): string {
  return module ? `${project}／${module}` : project;
}

/** code_inferred 是自己從程式推測的，在 RAG 標成 Inferred；其他都算已確認的內容。 */
function statusFor(confidence: string): NoteDoc['status'] {
  return confidence === 'code_inferred' ? 'Inferred' : 'Verified';
}

interface EntryRow {
  id: number;
  project: string;
  module: string | null;
  title: string;
  what_changed: string;
  why: string;
  logic_notes: string | null;
  data_notes: string | null;
  test_status: string;
  test_notes: string | null;
  how_to_explain: string | null;
  pr_url: string | null;
  happened_on: string;
  concepts: string[];
}

interface ConceptRow {
  id: number;
  term: string;
  plain_explanation: string;
  why_needed: string | null;
  example: string | null;
  where_in_system: string | null;
  open_questions: string | null;
  confidence: string;
  confirmed_by: string | null;
  related: string[];
}

interface RuleRow {
  project: string;
  module: string;
  title: string;
  body: string;
  confidence: string;
  confirmed_by: string | null;
}

interface ArticleRow {
  id: number;
  project: string;
  module: string | null;
  title: string;
  body: string;
  open_questions: string | null;
  confidence: string;
  confirmed_by: string | null;
  updated_at: string | Date;
  concepts: string[];
}

export function entryDoc(row: EntryRow): NoteDoc {
  const scope = scopeLabel(row.project, row.module);
  const answer = [
    `## ${row.title}`,
    `專案：${scope}　日期：${row.happened_on}　測試：${TEST_STATUS_LABEL[row.test_status] ?? row.test_status}`,
    '',
    ...section('改了什麼', row.what_changed),
    ...section('為什麼', row.why),
    ...section('邏輯判斷', row.logic_notes),
    ...section('資料面', row.data_notes),
    ...section('測試說明', row.test_notes),
    ...section('別人問時怎麼答', row.how_to_explain),
    ...(row.concepts.length > 0 ? [`相關觀念：${row.concepts.join('、')}`, ''] : []),
    ...(row.pr_url ? [`PR：${row.pr_url}`, ''] : []),
    markerFor('entry', row.id),
  ].join('\n');
  return {
    kind: 'entry',
    ref: String(row.id),
    title: row.title,
    question: truncate(`${row.title}（${scope}）`, QUESTION_MAX_CHARS),
    answer,
    status: 'Verified',
  };
}

export function conceptDoc(row: ConceptRow): NoteDoc {
  const confidence = CONFIDENCE_LABEL[row.confidence] ?? row.confidence;
  const answer = [
    `## ${row.term}`,
    `可信度：${confidence}${row.confirmed_by ? `（${row.confirmed_by}）` : ''}`,
    '',
    ...section('白話', row.plain_explanation),
    ...section('為什麼需要', row.why_needed),
    ...section('實際例子', row.example),
    ...section('系統裡在哪', row.where_in_system),
    ...section('還不懂的', row.open_questions),
    ...(row.related.length > 0 ? [`相關觀念：${row.related.join('、')}`, ''] : []),
    markerFor('concept', row.id),
  ].join('\n');
  return {
    kind: 'concept',
    ref: String(row.id),
    title: row.term,
    question: truncate(`${row.term}是什麼`, QUESTION_MAX_CHARS),
    answer,
    status: statusFor(row.confidence),
  };
}

export function ruleDoc(row: RuleRow): NoteDoc {
  const scope = scopeLabel(row.project, row.module);
  const ref = `${scope}／${row.title}`;
  const confidence = CONFIDENCE_LABEL[row.confidence] ?? row.confidence;
  const answer = [
    `## ${row.title}`,
    `規則：${scope}　可信度：${confidence}${row.confirmed_by ? `（${row.confirmed_by}）` : ''}`,
    '',
    row.body.trim(),
    '',
    markerFor('rule', ref),
  ].join('\n');
  return {
    kind: 'rule',
    ref,
    title: row.title,
    question: truncate(`${scope} 的規則：${row.title}`, QUESTION_MAX_CHARS),
    answer,
    status: statusFor(row.confidence),
  };
}

/** 一般筆記整篇放在答案裡；問題只放標題和範圍，使用者問得短也比對得到。 */
export function articleDoc(row: ArticleRow): NoteDoc {
  const scope = scopeLabel(row.project, row.module);
  const confidence = CONFIDENCE_LABEL[row.confidence] ?? row.confidence;
  const answer = [
    `## ${row.title}`,
    `筆記：${scope}　可信度：${confidence}${row.confirmed_by ? `（${row.confirmed_by}）` : ''}`,
    '',
    row.body.trim(),
    '',
    ...section('還不懂的', row.open_questions),
    ...(row.concepts.length > 0 ? [`相關觀念：${row.concepts.join('、')}`, ''] : []),
    markerFor('article', row.id),
  ].join('\n');
  return {
    kind: 'article',
    ref: String(row.id),
    title: row.title,
    question: truncate(`${row.title}（${scope}）`, QUESTION_MAX_CHARS),
    answer,
    status: statusFor(row.confidence),
  };
}

export interface LoadFilter {
  /** 要寫進公開 Space：拿掉只有擁有者該看到的資訊 */
  forPublic?: boolean;
  /** 只同步這些專案（不分大小寫）。觀念卡要指定為其中一個專案，或連到這些專案的改動紀錄。 */
  projects?: string[];
}

/** 讀出要同步的筆記：所有改動紀錄、觀念卡和現行規則（已停用的規則不算）。 */
export async function loadNoteDocs(q: Queryable, ownerId: number, filter: LoadFilter = {}): Promise<NoteDoc[]> {
  const projects = filter.projects?.map((p) => p.trim().toLowerCase()).filter(Boolean) ?? [];
  const byProject = projects.length > 0;

  const entries = await q.query<EntryRow>(
    `SELECT e.id, p.name AS project, m.name AS module, e.title, e.what_changed, e.why,
            e.logic_notes, e.data_notes, e.test_status, e.test_notes, e.how_to_explain,
            e.pr_url, e.happened_on::text,
            ARRAY(SELECT c.term FROM entry_concepts ec JOIN concepts c ON c.id = ec.concept_id
                  WHERE ec.entry_id = e.id ORDER BY c.term) AS concepts
     FROM entries e
     JOIN projects p ON p.id = e.project_id
     LEFT JOIN modules m ON m.id = e.module_id
     WHERE p.owner_id = $1 ${byProject ? 'AND lower(p.name) = ANY($2::text[])' : ''}
     ORDER BY e.happened_on DESC, e.id DESC`,
    byProject ? [ownerId, projects] : [ownerId],
  );

  const concepts = await q.query<ConceptRow>(
    `SELECT c.id, c.term, c.plain_explanation, c.why_needed, c.example, c.where_in_system,
            c.open_questions, c.confidence, c.confirmed_by,
            ARRAY(SELECT o.term FROM concept_relations r
                  JOIN concepts o ON o.id = CASE WHEN r.concept_a = c.id THEN r.concept_b ELSE r.concept_a END
                  WHERE c.id IN (r.concept_a, r.concept_b) ORDER BY o.term) AS related
     FROM concepts c
     WHERE c.owner_id = $1 ${
       byProject
         ? `AND EXISTS (SELECT 1 FROM projects p
                        WHERE lower(p.name) = ANY($2::text[]) AND ${CONCEPT_IN_PROJECT})`
         : ''
     }
     ORDER BY c.updated_at DESC, c.id DESC`,
    byProject ? [ownerId, projects] : [ownerId],
  );

  const rules = await q.query<RuleRow>(
    `SELECT p.name AS project, m.name AS module, r.title, r.body, r.confidence, r.confirmed_by
     FROM rules r
     JOIN modules m ON m.id = r.module_id
     JOIN projects p ON p.id = m.project_id
     WHERE r.retired_at IS NULL AND p.owner_id = $1 ${byProject ? 'AND lower(p.name) = ANY($2::text[])' : ''}
     ORDER BY p.name, m.name, r.title`,
    byProject ? [ownerId, projects] : [ownerId],
  );

  const articles = await q.query<ArticleRow>(
    `SELECT a.id, p.name AS project, m.name AS module, a.title, a.body, a.open_questions,
            a.confidence, a.confirmed_by, a.updated_at,
            ARRAY(SELECT c.term FROM article_concepts ac JOIN concepts c ON c.id = ac.concept_id
                  WHERE ac.article_id = a.id ORDER BY c.term) AS concepts
     FROM articles a
     JOIN projects p ON p.id = a.project_id
     LEFT JOIN modules m ON m.id = a.module_id
     WHERE p.owner_id = $1 ${byProject ? 'AND lower(p.name) = ANY($2::text[])' : ''}
     ORDER BY a.updated_at DESC, a.id DESC`,
    byProject ? [ownerId, projects] : [ownerId],
  );

  if (filter.forPublic) {
    // 公開內容不帶同事的名字，也不帶沒有一起公開的觀念名稱
    const shared = new Set(concepts.rows.map((c) => c.term));
    for (const entry of entries.rows) entry.concepts = entry.concepts.filter((term) => shared.has(term));
    for (const concept of concepts.rows) {
      concept.related = concept.related.filter((term) => shared.has(term));
      concept.confirmed_by = null;
    }
    for (const rule of rules.rows) rule.confirmed_by = null;
    for (const article of articles.rows) {
      article.concepts = article.concepts.filter((term) => shared.has(term));
      article.confirmed_by = null;
    }
  }

  return [
    ...entries.rows.map(entryDoc),
    ...concepts.rows.map(conceptDoc),
    ...rules.rows.map(ruleDoc),
    ...articles.rows.map(articleDoc),
  ];
}

/** 呼叫筆記 MCP 工具，回傳工具結果解析後的 JSON；工具回報錯誤時丟出錯誤，訊息是工具給的文字。 */
export type CallNotesTool = (name: string, args: Record<string, unknown>) => Promise<any>;

export function notesToolCaller(client: Client): CallNotesTool {
  return async (name, args) => {
    const result = await client.callTool({ name, arguments: args });
    const text = (result.content as Array<{ type: string; text?: string }>).find((c) => c.type === 'text')?.text ?? '';
    if (result.isError) throw new Error(text);
    return JSON.parse(text);
  };
}

/** MCP 一次最多回 50 筆，用 offset 一頁一頁讀完。 */
const MCP_PAGE = 50;

type McpConceptRow = ConceptRow & { updated_at: string; entry_ids: number[]; project?: string | null };

/**
 * 不連資料庫，改用筆記服務的 MCP 工具讀出和 loadNoteDocs 一樣的內容。
 *
 * 舊版伺服器不認得 offset，會一直回同一頁；遇到重複的 id 就停下來，complete 為 false，
 * 呼叫端不能把「沒讀到」當成「已刪除」。
 */
export async function loadNoteDocsViaMcp(
  call: CallNotesTool,
  filter: LoadFilter = {},
): Promise<{ docs: NoteDoc[]; complete: boolean }> {
  let complete = true;
  async function readAll<T extends { id: number }>(name: string, args: Record<string, unknown>, field: string) {
    const rows: T[] = [];
    const seen = new Set<number>();
    for (let offset = 0; ; offset += MCP_PAGE) {
      const page = (await call(name, { ...args, limit: MCP_PAGE, offset }))[field] as T[];
      if (page.some((row) => seen.has(row.id))) {
        complete = false;
        break;
      }
      for (const row of page) {
        seen.add(row.id);
        rows.push(row);
      }
      if (page.length < MCP_PAGE) break;
    }
    return rows;
  }

  const wanted = filter.projects?.map((p) => p.trim().toLowerCase()).filter(Boolean) ?? [];
  const byProject = wanted.length > 0;
  const projects = (await listProjectsViaMcp(call)).filter((p) => !byProject || wanted.includes(p.toLowerCase()));

  const entries: EntryRow[] = [];
  const rules: RuleRow[] = [];
  const articles: ArticleRow[] = [];
  for (const project of projects) {
    entries.push(...(await readAll<EntryRow>('search_entries', { project }, 'entries')));
    const result = (await call('get_project_rules', { project })) as { rules: Array<Omit<RuleRow, 'project'>> };
    rules.push(...result.rules.map((r) => ({ ...r, project })));
    articles.push(...(await readArticles(project)));
  }

  // 還沒有一般筆記的舊版伺服器沒有 search_articles：當成沒讀完，呼叫端就不會把問答上的筆記刪掉
  async function readArticles(project: string): Promise<ArticleRow[]> {
    try {
      return await readAll<ArticleRow>('search_articles', { project }, 'articles');
    } catch (err) {
      if (!/search_articles/.test(err instanceof Error ? err.message : '')) throw err;
      complete = false;
      return [];
    }
  }

  let concepts = await readAll<McpConceptRow>('search_concepts', {}, 'concepts');
  if (byProject) {
    const entryIds = new Set(entries.map((e) => e.id));
    concepts = concepts.filter(
      (c) => (c.project != null && wanted.includes(c.project.toLowerCase())) || c.entry_ids.some((id) => entryIds.has(id)),
    );
  }

  // 排序和資料庫版本一致，--limit 才會取到同樣的筆記
  entries.sort((a, b) => b.happened_on.localeCompare(a.happened_on) || b.id - a.id);
  concepts.sort((a, b) => String(b.updated_at).localeCompare(String(a.updated_at)) || b.id - a.id);
  articles.sort((a, b) => String(b.updated_at).localeCompare(String(a.updated_at)) || b.id - a.id);
  return {
    docs: [...entries.map(entryDoc), ...concepts.map(conceptDoc), ...rules.map(ruleDoc), ...articles.map(articleDoc)],
    complete,
  };
}

/**
 * MCP 沒有列出專案的工具，但 get_project_rules 查不到專案時，錯誤訊息會列出所有專案名稱，這裡借用那段訊息。
 * 回傳的名稱大小寫和資料庫一致，之後用來查詢和組規則的來源標記。
 */
async function listProjectsViaMcp(call: CallNotesTool): Promise<string[]> {
  try {
    await call('get_project_rules', { project: `__ragsync_list_projects_${Date.now()}__` });
  } catch (err) {
    const match = /現有專案：(.*)$/.exec(err instanceof Error ? err.message : '');
    if (match) return match[1] === '（還沒有專案）' ? [] : match[1]!.split('、');
  }
  throw new Error('無法從筆記 MCP 取得專案清單，get_project_rules 的錯誤訊息格式可能改了');
}

export interface SyncPlan {
  create: Array<QuestionPayload & { key: string; title: string }>;
  update: Array<QuestionPayload & { id: number; key: string; title: string }>;
  delete: Array<{ id: number; key: string }>;
  unchanged: number;
}

export function noteKey(doc: NoteDoc): string {
  return keyOf(doc.kind, doc.ref);
}

/**
 * 比對本地筆記和 RAG 上有來源標記的問答。
 * existingKeys 是目前所有筆記的 key（不受 --project、--limit 影響），RAG 上有、這裡沒有的才刪；
 * 沒給就不刪，用在無法確定讀到全部筆記的時候。
 */
export function buildPlan(
  docs: NoteDoc[],
  remote: RemoteQuestion[],
  itemIds: Record<NoteKind, number>,
  options: { existingKeys?: Set<string> },
): SyncPlan {
  const remoteByKey = new Map<string, RemoteQuestion>();
  const duplicates: RemoteQuestion[] = [];
  for (const question of remote) {
    const marker = parseMarker(question.answer);
    if (!marker) continue;
    const key = keyOf(marker.kind, marker.ref);
    if (remoteByKey.has(key)) duplicates.push(question);
    else remoteByKey.set(key, question);
  }

  const plan: SyncPlan = { create: [], update: [], delete: [], unchanged: 0 };
  const seen = new Set<string>();
  for (const doc of docs) {
    const key = noteKey(doc);
    seen.add(key);
    const payload = { qaItemTypeID: itemIds[doc.kind], question: doc.question, answer: doc.answer, status: doc.status };
    const existing = remoteByKey.get(key);
    if (!existing) {
      plan.create.push({ ...payload, key, title: doc.title });
    } else if (
      existing.qaItemTypeID !== payload.qaItemTypeID ||
      existing.question.trim() !== payload.question.trim() ||
      existing.answer.trim() !== payload.answer.trim() ||
      existing.status !== payload.status
    ) {
      plan.update.push({ ...payload, id: existing.id, key, title: doc.title });
    } else {
      plan.unchanged++;
    }
  }

  // 同一篇筆記在 RAG 有兩筆時（例如上次同步中斷後重跑），多的那筆一定刪
  for (const question of duplicates) {
    const marker = parseMarker(question.answer)!;
    plan.delete.push({ id: question.id, key: keyOf(marker.kind, marker.ref) });
  }
  if (options.existingKeys) {
    for (const [key, question] of remoteByKey) {
      if (!seen.has(key) && !options.existingKeys.has(key)) plan.delete.push({ id: question.id, key });
    }
  }
  return plan;
}

/** 找出分類的 id；create 為 false 時缺少的分類回傳 null，讓 dry-run 不會改到共用字典。 */
export async function resolveCategories(
  client: RagClient,
  create: boolean,
): Promise<Record<NoteKind, number> | null> {
  const findType = async () => (await client.listTypes()).find((t) => t.nameZh === RAG_CATEGORY.type);
  let type = await findType();
  if (!type) {
    if (!create) return null;
    await client.createType(RAG_CATEGORY.type);
    type = await findType();
    if (!type) throw new Error(`建立分類「${RAG_CATEGORY.type}」後找不到它`);
  }

  const kinds = Object.keys(RAG_CATEGORY.items) as NoteKind[];
  const findItems = async () => (await client.listItems()).filter((i) => i.qaTypeID === type.id);
  let items = await findItems();
  const missing = kinds.filter((k) => !items.some((i) => i.nameZh === RAG_CATEGORY.items[k]));
  if (missing.length > 0) {
    if (!create) return null;
    for (const kind of missing) await client.createItem(type.id, RAG_CATEGORY.items[kind]);
    items = await findItems();
  }

  const ids = {} as Record<NoteKind, number>;
  for (const kind of kinds) {
    const item = items.find((i) => i.nameZh === RAG_CATEGORY.items[kind]);
    if (!item) throw new Error(`建立子分類「${RAG_CATEGORY.items[kind]}」後找不到它`);
    ids[kind] = item.id;
  }
  return ids;
}

/** 一筆一筆送：RAG 每新增一筆都要算一次向量，失敗時停下來，已送出的下次同步會比對到。 */
export async function applyPlan(
  client: RagClient,
  plan: SyncPlan,
  onProgress: (message: string) => void = () => {},
): Promise<void> {
  for (const { key, title, ...payload } of plan.create) {
    await client.createQuestion(payload);
    onProgress(`新增 ${key} ${title}`);
  }
  for (const { key, title, ...payload } of plan.update) {
    await client.updateQuestion(payload);
    onProgress(`更新 ${key} ${title}`);
  }
  for (const { id, key } of plan.delete) {
    await client.deleteQuestion(id);
    onProgress(`刪除 ${key}`);
  }
}

/** 呼叫 RAG 的 .NET API（/api/v1）。不直接連向量服務，那邊需要內部金鑰。 */
export async function createHttpRagClient(options: {
  baseUrl: string;
  account?: string;
  password?: string;
  /** 背景同步代替使用者寫入時用，不需要密碼 */
  refreshToken?: string;
  spaceId?: number;
}): Promise<RagClient & { spaceName: string; refreshToken?: string }> {
  const base = `${options.baseUrl.replace(/\/+$/, '')}/api/v1`;

  async function send(method: string, path: string, body?: unknown, token?: string, spaceId?: number) {
    const headers: Record<string, string> = {};
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (token) headers.Authorization = `Bearer ${token}`;
    if (spaceId !== undefined) headers['X-Space-Id'] = String(spaceId);
    const res = await fetch(`${base}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await res.text();
    if (!res.ok) throw new Error(`RAG ${method} ${path} 失敗（${res.status}）：${truncate(text, 300)}`);
    return text ? (JSON.parse(text) as unknown) : null;
  }

  const login = (
    options.refreshToken
      ? await send('POST', '/Auth/Refresh', { refreshToken: options.refreshToken })
      : await send('POST', '/Auth/Login', { account: options.account, password: options.password })
  ) as { accessToken: string; refreshToken?: string };
  const token = login.accessToken;

  // 沒指定 Space 時用預設的（個人 Space），之後每個請求都明確帶上，避免送到別的 Space
  const spaces = (await send('GET', '/Space', undefined, token)) as Array<{
    spaceID: number;
    name: string;
    isDefault: boolean;
  }>;
  const space =
    options.spaceId !== undefined
      ? spaces.find((s) => s.spaceID === options.spaceId)
      : spaces.find((s) => s.isDefault) ?? (spaces.length === 1 ? spaces[0] : undefined);
  if (!space) {
    const names = spaces.map((s) => `${s.spaceID} ${s.name}`).join('、') || '（沒有任何 Space）';
    throw new Error(`找不到要同步的 Space，請設定 RAG_SPACE_ID。可用的 Space：${names}`);
  }

  const call = (method: string, path: string, body?: unknown) => send(method, path, body, token, space.spaceID);

  return {
    spaceName: `${space.spaceID} ${space.name}`,
    refreshToken: login.refreshToken,
    listTypes: async () => (await call('GET', '/QaType')) as Array<{ id: number; nameZh: string }>,
    createType: async (nameZh) => {
      await call('POST', '/QaType', { nameZh });
    },
    listItems: async () => (await call('GET', '/QaTypeItem')) as Array<{ id: number; qaTypeID: number; nameZh: string }>,
    createItem: async (qaTypeID, nameZh) => {
      await call('POST', '/QaTypeItem', { qaTypeID, nameZh });
    },
    // Type=0 是查全部；只會回傳目前 Space 裡自己的問答
    listQuestions: async () => (await call('GET', '/Question?Type=0')) as RemoteQuestion[],
    createQuestion: async (payload) => {
      await call('POST', '/Question', payload);
    },
    updateQuestion: async (payload) => {
      await call('PUT', '/Question', payload);
    },
    deleteQuestion: async (id) => {
      await call('DELETE', `/Question/${id}`);
    },
  };
}
