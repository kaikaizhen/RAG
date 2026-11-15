import type { Database, Queryable } from './db.js';
import { NotesError } from './errors.js';
import { assertNoSecrets } from './secretGuard.js';

export const CONFIDENCE = ['general_definition', 'code_inferred', 'colleague_confirmed'] as const;
export type Confidence = (typeof CONFIDENCE)[number];

export const TEST_STATUS = ['not_tested', 'manual', 'automated', 'production_verified'] as const;
export type TestStatus = (typeof TEST_STATUS)[number];

export const SHARING = ['off', 'private', 'public'] as const;
export type Sharing = (typeof SHARING)[number];

export interface AddEntryInput {
  project: string;
  module?: string;
  title: string;
  what_changed: string;
  why: string;
  logic_notes?: string;
  data_notes?: string;
  test_status?: TestStatus;
  test_notes?: string;
  how_to_explain?: string;
  pr_url?: string;
  happened_on?: string;
  concept_terms?: string[];
}

export interface UpdateEntryInput {
  id: number;
  title?: string;
  what_changed?: string;
  why?: string;
  logic_notes?: string;
  data_notes?: string;
  test_status?: TestStatus;
  test_notes?: string;
  how_to_explain?: string;
  pr_url?: string;
  happened_on?: string;
}

/** 可以修改的欄位；必填欄位不能清空，選填欄位傳空字串會清空。 */
const ENTRY_REQUIRED_FIELDS = ['title', 'what_changed', 'why', 'test_status', 'happened_on'] as const;
const ENTRY_OPTIONAL_FIELDS = ['logic_notes', 'data_notes', 'test_notes', 'how_to_explain', 'pr_url'] as const;

export interface SearchEntriesInput {
  query?: string;
  project?: string;
  module?: string;
  test_status?: TestStatus;
  /** 只取這一筆，給筆記網站的單篇連結用 */
  id?: number;
  limit?: number;
  offset?: number;
}

export interface RuleInput {
  title: string;
  body: string;
  confidence: Confidence;
  confirmed_by?: string;
}

export interface UpdateRulesInput {
  project: string;
  module: string;
  rules?: RuleInput[];
  retire_titles?: string[];
}

export interface SaveConceptInput {
  term: string;
  plain_explanation: string;
  confidence: Confidence;
  confirmed_by?: string;
  why_needed?: string;
  example?: string;
  where_in_system?: string;
  open_questions?: string;
  related_terms?: string[];
  link_entry_ids?: number[];
  /** 所屬專案：沒傳保留原本的，傳空字串取消 */
  project?: string;
}

export interface SearchConceptsInput {
  query?: string;
  confidence?: Confidence;
  has_open_questions?: boolean;
  /** 指定了所屬專案，或連到這個專案的改動紀錄 */
  project?: string;
  /** 只取這一張，給筆記網站的單篇連結用 */
  id?: number;
  limit?: number;
  offset?: number;
}

export interface SaveArticleInput {
  project: string;
  title: string;
  body: string;
  confidence: Confidence;
  confirmed_by?: string;
  /** 沒傳保留原本的，傳空字串清空 */
  module?: string;
  open_questions?: string;
  /** 有傳就整組換掉；沒傳保留原本的關聯 */
  concept_terms?: string[];
}

export interface SearchArticlesInput {
  query?: string;
  project?: string;
  module?: string;
  confidence?: Confidence;
  has_open_questions?: boolean;
  /** 只取這一篇，給筆記網站的單篇連結用 */
  id?: number;
  limit?: number;
  offset?: number;
}

/** 一篇筆記的內文上限。問答系統會整篇回傳，太長就該拆成幾篇。 */
export const ARTICLE_BODY_MAX = 20000;

export interface ListRulesInput {
  query?: string;
  project?: string;
  module?: string;
  confidence?: Confidence;
}

const DEFAULT_LIMIT = 10;
const MAX_LIMIT = 50;

/** 把使用者輸入當成字面文字，不讓 % 和 _ 變成萬用字元。 */
function likePattern(term: string): string {
  return `%${term.replace(/[\\%_]/g, '\\$&')}%`;
}

function searchTerms(query: string | undefined): string[] {
  return query?.trim().split(/\s+/).filter(Boolean) ?? [];
}

function clampLimit(limit: number | undefined): number {
  return Math.min(Math.max(limit ?? DEFAULT_LIMIT, 1), MAX_LIMIT);
}

/** 可信度是「已向同事確認」時一定要有確認者，其他可信度一律不留確認者。 */
function confirmedByFor(subject: string, confidence: Confidence, confirmedBy?: string): string | null {
  const name = confirmedBy?.trim();
  if (confidence === 'colleague_confirmed') {
    if (!name) throw new NotesError(`「${subject}」標記為已向同事確認，請填 confirmed_by（向誰確認）`);
    return name;
  }
  if (name) {
    throw new NotesError(`「${subject}」有填 confirmed_by，但可信度是 ${confidence}。確認過請改成 colleague_confirmed`);
  }
  return null;
}

async function ensureProject(q: Queryable, ownerId: number, name: string) {
  const clean = name.trim();
  const inserted = await q.query<{ id: number }>(
    `INSERT INTO projects (owner_id, name) VALUES ($1, $2)
     ON CONFLICT (owner_id, lower(name)) DO NOTHING RETURNING id`,
    [ownerId, clean],
  );
  if (inserted.rows[0]) return { id: inserted.rows[0].id, created: true };
  const { rows } = await q.query<{ id: number }>(
    'SELECT id FROM projects WHERE owner_id = $1 AND lower(name) = lower($2)',
    [ownerId, clean],
  );
  return { id: rows[0]!.id, created: false };
}

async function ensureModule(q: Queryable, projectId: number, name: string) {
  const clean = name.trim();
  const inserted = await q.query<{ id: number }>(
    `INSERT INTO modules (project_id, name) VALUES ($1, $2)
     ON CONFLICT (project_id, lower(name)) DO NOTHING RETURNING id`,
    [projectId, clean],
  );
  if (inserted.rows[0]) return { id: inserted.rows[0].id, created: true };
  const { rows } = await q.query<{ id: number }>(
    'SELECT id FROM modules WHERE project_id = $1 AND lower(name) = lower($2)',
    [projectId, clean],
  );
  return { id: rows[0]!.id, created: false };
}

/** 查不到專案時列出現有名稱，讓 Claude 看得出是不是打錯字。 */
async function requireProject(q: Queryable, ownerId: number, name: string): Promise<number> {
  const { rows } = await q.query<{ id: number }>(
    'SELECT id FROM projects WHERE owner_id = $1 AND lower(name) = lower($2)',
    [ownerId, name.trim()],
  );
  if (rows[0]) return rows[0].id;
  const all = await q.query<{ name: string }>('SELECT name FROM projects WHERE owner_id = $1 ORDER BY name', [ownerId]);
  const names = all.rows.map((r) => r.name).join('、') || '（還沒有專案）';
  throw new NotesError(`找不到專案「${name}」。現有專案：${names}`);
}

async function findConcepts(q: Queryable, ownerId: number, terms: string[]) {
  const { rows } = await q.query<{ id: number; term: string }>(
    `SELECT id, term FROM concepts
     WHERE owner_id = $1 AND lower(term) IN (SELECT lower(t) FROM unnest($2::text[]) AS t)`,
    [ownerId, terms],
  );
  const found = new Set(rows.map((r) => r.term.toLowerCase()));
  return { rows, unresolved: terms.filter((t) => !found.has(t.toLowerCase())) };
}

/** 回傳的物件只讀寫 ownerId 這位使用者的筆記。 */
export function createNotes(db: Database, ownerId: number) {
  return {
    async addEntry(input: AddEntryInput) {
      assertNoSecrets(input);
      return db.tx(async (q) => {
        const project = await ensureProject(q, ownerId, input.project);
        const module = input.module ? await ensureModule(q, project.id, input.module) : null;

        const { rows } = await q.query<{ id: number; happened_on: string }>(
          `INSERT INTO entries (project_id, module_id, title, what_changed, why, logic_notes, data_notes,
                                test_status, test_notes, how_to_explain, pr_url, happened_on)
           VALUES ($1, $2, $3, $4, $5, $6, $7, COALESCE($8::text, 'not_tested'), $9, $10, $11,
                   COALESCE($12::date, (now() AT TIME ZONE 'Asia/Taipei')::date))
           RETURNING id, happened_on::text`,
          [
            project.id,
            module?.id ?? null,
            input.title,
            input.what_changed,
            input.why,
            input.logic_notes ?? null,
            input.data_notes ?? null,
            input.test_status ?? null,
            input.test_notes ?? null,
            input.how_to_explain ?? null,
            input.pr_url ?? null,
            input.happened_on ?? null,
          ],
        );
        const entry = rows[0]!;

        const concepts = await findConcepts(q, ownerId, input.concept_terms ?? []);
        for (const concept of concepts.rows) {
          await q.query('INSERT INTO entry_concepts (entry_id, concept_id) VALUES ($1, $2)', [entry.id, concept.id]);
        }

        return {
          id: entry.id,
          happened_on: entry.happened_on,
          project_created: project.created,
          module_created: module?.created ?? false,
          linked_concepts: concepts.rows.map((c) => c.term),
          unresolved_concept_terms: concepts.unresolved,
        };
      });
    },

    async searchEntries(input: SearchEntriesInput) {
      const params: unknown[] = [];
      const add = (value: unknown) => `$${params.push(value)}`;
      const where = [`p.owner_id = ${add(ownerId)}`];

      const terms = searchTerms(input.query);
      for (const term of terms) where.push(`e.search_text ILIKE ${add(likePattern(term))}`);
      if (input.project) where.push(`lower(p.name) = lower(${add(input.project.trim())})`);
      if (input.module) where.push(`lower(m.name) = lower(${add(input.module.trim())})`);
      if (input.test_status) where.push(`e.test_status = ${add(input.test_status)}`);
      if (input.id !== undefined) where.push(`e.id = ${add(input.id)}`);

      const rank = terms.length > 0 ? `similarity(e.search_text, ${add(input.query!.trim())}) DESC, ` : '';
      const limit = add(clampLimit(input.limit));
      const offset = add(Math.max(input.offset ?? 0, 0));

      const { rows } = await db.query(
        `SELECT e.id, p.name AS project, m.name AS module, e.title, e.what_changed, e.why,
                e.logic_notes, e.data_notes, e.test_status, e.test_notes, e.how_to_explain,
                e.pr_url, e.happened_on::text,
                ARRAY(SELECT c.term FROM entry_concepts ec JOIN concepts c ON c.id = ec.concept_id
                      WHERE ec.entry_id = e.id ORDER BY c.term) AS concepts
         FROM entries e
         JOIN projects p ON p.id = e.project_id
         LEFT JOIN modules m ON m.id = e.module_id
         WHERE ${where.join(' AND ')}
         ORDER BY ${rank}e.happened_on DESC, e.id DESC
         LIMIT ${limit} OFFSET ${offset}`,
        params,
      );
      return { count: rows.length, entries: rows };
    },

    /**
     * 修正寫錯的改動紀錄。改動紀錄是專案的歷史，之後又改了程式要另記一筆，這裡只用來更正內容。
     * 沒傳的欄位不變。
     */
    async updateEntry(input: UpdateEntryInput) {
      assertNoSecrets(input);
      const params: unknown[] = [input.id, ownerId];
      const sets: string[] = [];
      const fields: string[] = [];
      const add = (value: unknown) => `$${params.push(value)}`;

      for (const field of ENTRY_REQUIRED_FIELDS) {
        const value = input[field];
        if (value === undefined) continue;
        if (!value.trim()) throw new NotesError(`${field} 是必填欄位，不能清空`);
        sets.push(`${field} = ${add(value)}${field === 'happened_on' ? '::date' : ''}`);
        fields.push(field);
      }
      for (const field of ENTRY_OPTIONAL_FIELDS) {
        const value = input[field];
        if (value === undefined) continue;
        sets.push(`${field} = NULLIF(${add(value)}::text, '')`);
        fields.push(field);
      }
      if (sets.length === 0) throw new NotesError('沒有要修改的欄位');

      const { rows } = await db.query<{ id: number; title: string }>(
        `UPDATE entries e SET ${sets.join(', ')} FROM projects p
         WHERE e.id = $1 AND p.id = e.project_id AND p.owner_id = $2
         RETURNING e.id, e.title`,
        params,
      );
      if (!rows[0]) throw new NotesError(`找不到改動紀錄 id：${input.id}`);
      return { ...rows[0], updated_fields: fields };
    },

    /** 刪除重複或記錯的改動紀錄，連同它和觀念卡的關聯；觀念卡本身不動。 */
    async deleteEntry(input: { id: number }) {
      return db.tx(async (q) => {
        const owned = await q.query(
          'SELECT 1 FROM entries e JOIN projects p ON p.id = e.project_id WHERE e.id = $1 AND p.owner_id = $2',
          [input.id, ownerId],
        );
        if (!owned.rows[0]) throw new NotesError(`找不到改動紀錄 id：${input.id}`);
        await q.query('DELETE FROM entry_concepts WHERE entry_id = $1', [input.id]);
        const { rows } = await q.query<{ id: number; title: string }>(
          'DELETE FROM entries WHERE id = $1 RETURNING id, title',
          [input.id],
        );
        return { deleted: rows[0] };
      });
    },

    async getProjectRules(input: { project: string; module?: string }) {
      const projectId = await requireProject(db, ownerId, input.project);
      const params: unknown[] = [projectId];
      let moduleFilter = '';
      if (input.module) {
        params.push(input.module.trim());
        moduleFilter = 'AND lower(m.name) = lower($2)';
      }
      const { rows } = await db.query(
        `SELECT m.name AS module, r.title, r.body, r.confidence, r.confirmed_by, r.updated_at
         FROM rules r JOIN modules m ON m.id = r.module_id
         WHERE m.project_id = $1 AND r.retired_at IS NULL ${moduleFilter}
         ORDER BY m.name, r.title`,
        params,
      );
      return { project: input.project.trim(), count: rows.length, rules: rows };
    },

    async updateProjectRules(input: UpdateRulesInput) {
      assertNoSecrets(input);
      const rules = input.rules ?? [];
      const retire = input.retire_titles ?? [];
      if (rules.length === 0 && retire.length === 0) {
        throw new NotesError('rules 和 retire_titles 至少要有一個');
      }
      const prepared = rules.map((rule) => ({
        ...rule,
        confirmedBy: confirmedByFor(rule.title, rule.confidence, rule.confirmed_by),
      }));

      return db.tx(async (q) => {
        const project = await ensureProject(q, ownerId, input.project);
        const module = await ensureModule(q, project.id, input.module);

        const saved = [];
        for (const rule of prepared) {
          const { rows } = await q.query<{ title: string; created: boolean }>(
            `INSERT INTO rules (module_id, title, body, confidence, confirmed_by)
             VALUES ($1, $2, $3, $4, $5)
             ON CONFLICT (module_id, lower(title)) DO UPDATE SET
               title = EXCLUDED.title, body = EXCLUDED.body, confidence = EXCLUDED.confidence,
               confirmed_by = EXCLUDED.confirmed_by, retired_at = NULL, updated_at = now()
             RETURNING title, (xmax = 0) AS created`,
            [module.id, rule.title.trim(), rule.body, rule.confidence, rule.confirmedBy],
          );
          saved.push(rows[0]!);
        }

        const { rows: retired } = await q.query<{ title: string }>(
          `UPDATE rules SET retired_at = now(), updated_at = now()
           WHERE module_id = $1 AND retired_at IS NULL
             AND lower(title) IN (SELECT lower(t) FROM unnest($2::text[]) AS t)
           RETURNING title`,
          [module.id, retire],
        );
        const retiredSet = new Set(retired.map((r) => r.title.toLowerCase()));

        return {
          project_created: project.created,
          module_created: module.created,
          saved,
          retired: retired.map((r) => r.title),
          retire_not_found: retire.filter((t) => !retiredSet.has(t.toLowerCase())),
        };
      });
    },

    async saveConcept(input: SaveConceptInput) {
      assertNoSecrets(input);
      const term = input.term.trim();
      const confirmedBy = confirmedByFor(term, input.confidence, input.confirmed_by);

      return db.tx(async (q) => {
        // 只接受已經有的專案，打錯字不會悄悄多出一個專案
        const projectName = input.project?.trim();
        const projectId = projectName ? await requireProject(q, ownerId, projectName) : null;

        // 選填欄位沒傳就保留舊值，傳空字串才清空。
        const { rows } = await q.query<{ id: number; created: boolean }>(
          `INSERT INTO concepts (term, plain_explanation, confidence, confirmed_by,
                                 why_needed, example, where_in_system, open_questions, owner_id, project_id)
           VALUES ($1, $2, $3, $4, NULLIF($5::text, ''), NULLIF($6::text, ''), NULLIF($7::text, ''), NULLIF($8::text, ''), $9, $10)
           ON CONFLICT (owner_id, lower(term)) DO UPDATE SET
             term = EXCLUDED.term,
             plain_explanation = EXCLUDED.plain_explanation,
             confidence = EXCLUDED.confidence,
             confirmed_by = EXCLUDED.confirmed_by,
             why_needed = CASE WHEN $5::text IS NULL THEN concepts.why_needed ELSE EXCLUDED.why_needed END,
             example = CASE WHEN $6::text IS NULL THEN concepts.example ELSE EXCLUDED.example END,
             where_in_system = CASE WHEN $7::text IS NULL THEN concepts.where_in_system ELSE EXCLUDED.where_in_system END,
             open_questions = CASE WHEN $8::text IS NULL THEN concepts.open_questions ELSE EXCLUDED.open_questions END,
             project_id = CASE WHEN $11::boolean THEN EXCLUDED.project_id ELSE concepts.project_id END,
             updated_at = now()
           RETURNING id, (xmax = 0) AS created`,
          [
            term,
            input.plain_explanation,
            input.confidence,
            confirmedBy,
            input.why_needed ?? null,
            input.example ?? null,
            input.where_in_system ?? null,
            input.open_questions ?? null,
            ownerId,
            projectId,
            input.project !== undefined,
          ],
        );
        const concept = rows[0]!;

        const related = await findConcepts(q, ownerId, input.related_terms ?? []);
        for (const other of related.rows) {
          if (other.id === concept.id) continue;
          await q.query(
            `INSERT INTO concept_relations (concept_a, concept_b)
             VALUES (LEAST($1::int, $2::int), GREATEST($1::int, $2::int)) ON CONFLICT DO NOTHING`,
            [concept.id, other.id],
          );
        }

        const entryIds = [...new Set(input.link_entry_ids ?? [])];
        if (entryIds.length > 0) {
          const found = await q.query<{ id: number }>(
            `SELECT e.id FROM entries e JOIN projects p ON p.id = e.project_id
             WHERE e.id = ANY($1::int[]) AND p.owner_id = $2`,
            [entryIds, ownerId],
          );
          const known = new Set(found.rows.map((r) => r.id));
          const missing = entryIds.filter((id) => !known.has(id));
          if (missing.length > 0) throw new NotesError(`找不到改動紀錄 id：${missing.join('、')}，整筆沒有寫入`);
          for (const entryId of entryIds) {
            await q.query(
              'INSERT INTO entry_concepts (entry_id, concept_id) VALUES ($1, $2) ON CONFLICT DO NOTHING',
              [entryId, concept.id],
            );
          }
        }

        return {
          id: concept.id,
          created: concept.created,
          related: related.rows.filter((r) => r.id !== concept.id).map((r) => r.term),
          unresolved_related_terms: related.unresolved,
          linked_entry_ids: entryIds,
          ...(input.project !== undefined && { project: projectName || null }),
        };
      });
    },

    async searchConcepts(input: SearchConceptsInput) {
      const params: unknown[] = [];
      const add = (value: unknown) => `$${params.push(value)}`;
      const where = [`c.owner_id = ${add(ownerId)}`];

      const terms = searchTerms(input.query);
      for (const term of terms) where.push(`c.search_text ILIKE ${add(likePattern(term))}`);
      if (input.confidence) where.push(`c.confidence = ${add(input.confidence)}`);
      if (input.has_open_questions === true) where.push('c.open_questions IS NOT NULL');
      if (input.has_open_questions === false) where.push('c.open_questions IS NULL');
      if (input.id !== undefined) where.push(`c.id = ${add(input.id)}`);
      if (input.project) {
        where.push(
          `EXISTS (SELECT 1 FROM projects p WHERE lower(p.name) = lower(${add(input.project.trim())}) AND ${CONCEPT_IN_PROJECT})`,
        );
      }

      const rank = terms.length > 0 ? `similarity(c.search_text, ${add(input.query!.trim())}) DESC, ` : '';
      const limit = add(clampLimit(input.limit));
      const offset = add(Math.max(input.offset ?? 0, 0));

      const { rows } = await db.query(
        `SELECT c.id, c.term, c.plain_explanation, c.why_needed, c.example, c.where_in_system,
                c.open_questions, c.confidence, c.confirmed_by, c.updated_at,
                (SELECT p.name FROM projects p WHERE p.id = c.project_id) AS project,
                ARRAY(SELECT o.term FROM concept_relations r
                      JOIN concepts o ON o.id = CASE WHEN r.concept_a = c.id THEN r.concept_b ELSE r.concept_a END
                      WHERE c.id IN (r.concept_a, r.concept_b) ORDER BY o.term) AS related,
                ARRAY(SELECT ec.entry_id FROM entry_concepts ec
                      WHERE ec.concept_id = c.id ORDER BY ec.entry_id) AS entry_ids
         FROM concepts c
         WHERE ${where.join(' AND ')}
         ORDER BY ${rank}c.updated_at DESC, c.id DESC
         LIMIT ${limit} OFFSET ${offset}`,
        params,
      );
      return { count: rows.length, concepts: rows };
    },

    /** 刪除重複或記錯的觀念卡，連同它和改動紀錄、其他觀念的關聯；改動紀錄本身不動。 */
    async deleteConcept(input: { term: string }) {
      return db.tx(async (q) => {
        const { rows } = await q.query<{ id: number; term: string }>(
          'SELECT id, term FROM concepts WHERE owner_id = $1 AND lower(term) = lower($2)',
          [ownerId, input.term.trim()],
        );
        const concept = rows[0];
        if (!concept) throw new NotesError(`找不到觀念卡「${input.term}」`);
        await q.query('DELETE FROM entry_concepts WHERE concept_id = $1', [concept.id]);
        await q.query('DELETE FROM article_concepts WHERE concept_id = $1', [concept.id]);
        await q.query('DELETE FROM concept_relations WHERE $1 IN (concept_a, concept_b)', [concept.id]);
        await q.query('DELETE FROM concepts WHERE id = $1', [concept.id]);
        return { deleted: concept };
      });
    },

    /**
     * 新增或更新一般筆記，同一個專案裡以標題判斷是不是同一篇。
     * 專案不存在時會自動建立（和改動紀錄一樣），回傳 project_created 讓 Claude 確認不是打錯字。
     */
    async saveArticle(input: SaveArticleInput) {
      assertNoSecrets(input);
      const title = input.title.trim();
      if (input.body.length > ARTICLE_BODY_MAX) {
        throw new NotesError(`內文超過 ${ARTICLE_BODY_MAX} 字，請拆成幾篇，問答系統才找得準`);
      }
      const confirmedBy = confirmedByFor(title, input.confidence, input.confirmed_by);

      return db.tx(async (q) => {
        const project = await ensureProject(q, ownerId, input.project);
        const moduleName = input.module?.trim();
        const module = moduleName ? await ensureModule(q, project.id, moduleName) : null;

        const { rows } = await q.query<{ id: number; created: boolean }>(
          `INSERT INTO articles (project_id, module_id, title, body, open_questions, confidence, confirmed_by)
           VALUES ($1, $2, $3, $4, NULLIF($5::text, ''), $6, $7)
           ON CONFLICT (project_id, lower(title)) DO UPDATE SET
             title = EXCLUDED.title,
             body = EXCLUDED.body,
             confidence = EXCLUDED.confidence,
             confirmed_by = EXCLUDED.confirmed_by,
             module_id = CASE WHEN $8::boolean THEN EXCLUDED.module_id ELSE articles.module_id END,
             open_questions = CASE WHEN $5::text IS NULL THEN articles.open_questions ELSE EXCLUDED.open_questions END,
             updated_at = now()
           RETURNING id, (xmax = 0) AS created`,
          [
            project.id,
            module?.id ?? null,
            title,
            input.body,
            input.open_questions ?? null,
            input.confidence,
            confirmedBy,
            input.module !== undefined,
          ],
        );
        const article = rows[0]!;

        let concepts: { rows: { id: number; term: string }[]; unresolved: string[] } = { rows: [], unresolved: [] };
        if (input.concept_terms !== undefined) {
          concepts = await findConcepts(q, ownerId, input.concept_terms);
          await q.query('DELETE FROM article_concepts WHERE article_id = $1', [article.id]);
          for (const concept of concepts.rows) {
            await q.query('INSERT INTO article_concepts (article_id, concept_id) VALUES ($1, $2)', [article.id, concept.id]);
          }
        }

        return {
          id: article.id,
          created: article.created,
          project_created: project.created,
          module_created: module?.created ?? false,
          linked_concepts: concepts.rows.map((c) => c.term),
          unresolved_concept_terms: concepts.unresolved,
        };
      });
    },

    async searchArticles(input: SearchArticlesInput) {
      const params: unknown[] = [];
      const add = (value: unknown) => `$${params.push(value)}`;
      const where = [`p.owner_id = ${add(ownerId)}`];

      const terms = searchTerms(input.query);
      for (const term of terms) where.push(`a.search_text ILIKE ${add(likePattern(term))}`);
      if (input.project) where.push(`lower(p.name) = lower(${add(input.project.trim())})`);
      if (input.module) where.push(`lower(m.name) = lower(${add(input.module.trim())})`);
      if (input.confidence) where.push(`a.confidence = ${add(input.confidence)}`);
      if (input.has_open_questions === true) where.push('a.open_questions IS NOT NULL');
      if (input.has_open_questions === false) where.push('a.open_questions IS NULL');
      if (input.id !== undefined) where.push(`a.id = ${add(input.id)}`);

      const rank = terms.length > 0 ? `similarity(a.search_text, ${add(input.query!.trim())}) DESC, ` : '';
      const limit = add(clampLimit(input.limit));
      const offset = add(Math.max(input.offset ?? 0, 0));

      const { rows } = await db.query(
        `SELECT a.id, p.name AS project, m.name AS module, a.title, a.body, a.open_questions,
                a.confidence, a.confirmed_by, a.created_at, a.updated_at,
                ARRAY(SELECT c.term FROM article_concepts ac JOIN concepts c ON c.id = ac.concept_id
                      WHERE ac.article_id = a.id ORDER BY c.term) AS concepts
         FROM articles a
         JOIN projects p ON p.id = a.project_id
         LEFT JOIN modules m ON m.id = a.module_id
         WHERE ${where.join(' AND ')}
         ORDER BY ${rank}a.updated_at DESC, a.id DESC
         LIMIT ${limit} OFFSET ${offset}`,
        params,
      );
      return { count: rows.length, articles: rows };
    },

    /** 刪除記錯或重複的一般筆記，連同它和觀念卡的關聯；觀念卡本身不動。 */
    async deleteArticle(input: { id: number }) {
      return db.tx(async (q) => {
        const owned = await q.query(
          'SELECT 1 FROM articles a JOIN projects p ON p.id = a.project_id WHERE a.id = $1 AND p.owner_id = $2',
          [input.id, ownerId],
        );
        if (!owned.rows[0]) throw new NotesError(`找不到筆記 id：${input.id}`);
        await q.query('DELETE FROM article_concepts WHERE article_id = $1', [input.id]);
        const { rows } = await q.query<{ id: number; title: string }>(
          'DELETE FROM articles WHERE id = $1 RETURNING id, title',
          [input.id],
        );
        return { deleted: rows[0] };
      });
    },

    /** 只有觀念卡或規則、還沒有改動紀錄的專案也要列出來，所以三種筆數都算。 */
    async listProjects() {
      const { rows } = await db.query(
        `SELECT p.name,
                ARRAY(SELECT m.name FROM modules m WHERE m.project_id = p.id ORDER BY m.name) AS modules,
                (SELECT count(*)::int FROM entries e WHERE e.project_id = p.id) AS entry_count,
                (SELECT count(*)::int FROM concepts c WHERE ${CONCEPT_IN_PROJECT}) AS concept_count,
                (SELECT count(*)::int FROM rules r JOIN modules m ON m.id = r.module_id
                 WHERE m.project_id = p.id AND r.retired_at IS NULL) AS rule_count,
                (SELECT count(*)::int FROM articles a WHERE a.project_id = p.id) AS article_count
         FROM projects p WHERE p.owner_id = $1 ORDER BY p.name`,
        [ownerId],
      );
      return { projects: rows };
    },

    /** 總覽頁的數字。可信度同時算觀念卡、現行規則和一般筆記，已停用的規則不算。 */
    async getStats() {
      const { rows } = await db.query<Record<string, number>>(
        `WITH my_concepts AS (SELECT confidence, open_questions FROM concepts WHERE owner_id = $1),
              my_rules AS (
                SELECT r.confidence FROM rules r
                JOIN modules m ON m.id = r.module_id
                JOIN projects p ON p.id = m.project_id
                WHERE r.retired_at IS NULL AND p.owner_id = $1
              ),
              my_articles AS (
                SELECT a.confidence FROM articles a JOIN projects p ON p.id = a.project_id WHERE p.owner_id = $1
              ),
              confidences AS (
                SELECT confidence FROM my_concepts UNION ALL SELECT confidence FROM my_rules
                UNION ALL SELECT confidence FROM my_articles
              )
         SELECT
           (SELECT count(*)::int FROM entries e JOIN projects p ON p.id = e.project_id WHERE p.owner_id = $1) AS entries,
           (SELECT count(*)::int FROM my_articles) AS articles,
           (SELECT count(*)::int FROM my_concepts) AS concepts,
           (SELECT count(*)::int FROM my_rules) AS rules,
           (SELECT count(*)::int FROM my_concepts WHERE open_questions IS NOT NULL) AS open_questions,
           (SELECT count(*)::int FROM confidences WHERE confidence = 'general_definition') AS general_definition,
           (SELECT count(*)::int FROM confidences WHERE confidence = 'code_inferred') AS code_inferred,
           (SELECT count(*)::int FROM confidences WHERE confidence = 'colleague_confirmed') AS colleague_confirmed`,
        [ownerId],
      );
      return rows[0]!;
    },

    /** 匯出自己的全部筆記，包含已停用的規則和已解決的缺口，讓使用者可以完整帶走。 */
    async exportAll() {
      const projects = await db.query(
        `SELECT p.name, p.sharing,
                ARRAY(SELECT m.name FROM modules m WHERE m.project_id = p.id ORDER BY m.name) AS modules
         FROM projects p WHERE p.owner_id = $1 ORDER BY p.name`,
        [ownerId],
      );
      const entries = await db.query(
        `SELECT e.id, p.name AS project, m.name AS module, e.title, e.what_changed, e.why,
                e.logic_notes, e.data_notes, e.test_status, e.test_notes, e.how_to_explain,
                e.pr_url, e.happened_on::text, e.created_at,
                ARRAY(SELECT c.term FROM entry_concepts ec JOIN concepts c ON c.id = ec.concept_id
                      WHERE ec.entry_id = e.id ORDER BY c.term) AS concepts
         FROM entries e
         JOIN projects p ON p.id = e.project_id
         LEFT JOIN modules m ON m.id = e.module_id
         WHERE p.owner_id = $1
         ORDER BY e.happened_on, e.id`,
        [ownerId],
      );
      const concepts = await db.query(
        `SELECT c.id, c.term, (SELECT p.name FROM projects p WHERE p.id = c.project_id) AS project,
                c.plain_explanation, c.why_needed, c.example, c.where_in_system, c.open_questions,
                c.confidence, c.confirmed_by, c.created_at, c.updated_at,
                ARRAY(SELECT o.term FROM concept_relations r
                      JOIN concepts o ON o.id = CASE WHEN r.concept_a = c.id THEN r.concept_b ELSE r.concept_a END
                      WHERE c.id IN (r.concept_a, r.concept_b) ORDER BY o.term) AS related,
                ARRAY(SELECT ec.entry_id FROM entry_concepts ec WHERE ec.concept_id = c.id ORDER BY ec.entry_id) AS entry_ids
         FROM concepts c WHERE c.owner_id = $1 ORDER BY c.term`,
        [ownerId],
      );
      const rules = await db.query(
        `SELECT p.name AS project, m.name AS module, r.title, r.body, r.confidence, r.confirmed_by,
                r.created_at, r.updated_at, r.retired_at
         FROM rules r
         JOIN modules m ON m.id = r.module_id
         JOIN projects p ON p.id = m.project_id
         WHERE p.owner_id = $1
         ORDER BY p.name, m.name, r.title`,
        [ownerId],
      );
      const articles = await db.query(
        `SELECT a.id, p.name AS project, m.name AS module, a.title, a.body, a.open_questions,
                a.confidence, a.confirmed_by, a.created_at, a.updated_at,
                ARRAY(SELECT c.term FROM article_concepts ac JOIN concepts c ON c.id = ac.concept_id
                      WHERE ac.article_id = a.id ORDER BY c.term) AS concepts
         FROM articles a
         JOIN projects p ON p.id = a.project_id
         LEFT JOIN modules m ON m.id = a.module_id
         WHERE p.owner_id = $1
         ORDER BY p.name, a.title`,
        [ownerId],
      );
      const gaps = await db.query(
        `SELECT question, resolution, created_at, resolved_at FROM knowledge_gaps
         WHERE owner_id = $1 ORDER BY created_at, id`,
        [ownerId],
      );
      return {
        format: 'learning-notes-export',
        version: 1,
        exported_at: new Date().toISOString(),
        projects: projects.rows,
        entries: entries.rows,
        concepts: concepts.rows,
        rules: rules.rows,
        articles: articles.rows,
        knowledge_gaps: gaps.rows,
      };
    },

    async listSharing() {
      const { rows } = await db.query(
        `SELECT p.name, p.sharing,
                (SELECT count(*)::int FROM entries e WHERE e.project_id = p.id) AS entry_count,
                (SELECT count(*)::int FROM rules r JOIN modules m ON m.id = r.module_id
                 WHERE m.project_id = p.id AND r.retired_at IS NULL) AS rule_count,
                (SELECT count(*)::int FROM concepts c WHERE ${CONCEPT_IN_PROJECT}) AS concept_count,
                (SELECT count(*)::int FROM articles a WHERE a.project_id = p.id) AS article_count
         FROM projects p WHERE p.owner_id = $1 ORDER BY p.name`,
        [ownerId],
      );
      // 沒有所屬專案、也沒連到任何紀錄的觀念卡，不管怎麼設定都不會同步
      const unassigned = await db.query<{ count: number }>(
        `SELECT count(*)::int AS count FROM concepts c
         WHERE c.owner_id = $1 AND c.project_id IS NULL
           AND NOT EXISTS (SELECT 1 FROM entry_concepts ec WHERE ec.concept_id = c.id)`,
        [ownerId],
      );
      return { projects: rows, unassigned_concepts: unassigned.rows[0]!.count };
    },

    async setSharing(input: { project: string; sharing: Sharing }) {
      return db.tx(async (q) => {
        const projectId = await requireProject(q, ownerId, input.project);
        if (input.sharing === 'public') {
          const taken = await q.query(
            `SELECT 1 FROM projects WHERE sharing = 'public' AND lower(name) = lower($1) AND id <> $2`,
            [input.project.trim(), projectId],
          );
          if (taken.rows[0]) throw new NotesError('已經有同名的公開專案，請換個專案名稱再公開');
          await assertPublishable(q, projectId);
        }
        await q.query('UPDATE projects SET sharing = $2 WHERE id = $1', [projectId, input.sharing]);
        return { project: input.project.trim(), sharing: input.sharing };
      });
    },

    /** 問答答不出來的問題先記下來，同一題還沒解決時不重複。 */
    async addGap(input: { question: string }) {
      assertNoSecrets(input);
      const question = input.question.trim();
      if (!question) throw new NotesError('問題不能是空的');
      const inserted = await db.query<{ id: number }>(
        `INSERT INTO knowledge_gaps (owner_id, question) VALUES ($1, $2)
         ON CONFLICT (owner_id, lower(question)) WHERE resolved_at IS NULL DO NOTHING RETURNING id`,
        [ownerId, question],
      );
      return { added: inserted.rows.length > 0, question };
    },

    async listGaps(input: { limit?: number } = {}) {
      const { rows } = await db.query<{ id: number; question: string; created_at: Date }>(
        `SELECT id, question, created_at FROM knowledge_gaps
         WHERE owner_id = $1 AND resolved_at IS NULL ORDER BY created_at DESC, id DESC LIMIT $2`,
        [ownerId, clampLimit(input.limit ?? MAX_LIMIT)],
      );
      return { count: rows.length, gaps: rows };
    },

    async resolveGap(input: { id: number; resolution?: string }) {
      assertNoSecrets(input);
      const { rows } = await db.query<{ id: number; question: string }>(
        `UPDATE knowledge_gaps SET resolved_at = now(), resolution = NULLIF($3::text, '')
         WHERE id = $1 AND owner_id = $2 AND resolved_at IS NULL RETURNING id, question`,
        [input.id, ownerId, input.resolution?.trim() ?? ''],
      );
      if (!rows[0]) throw new NotesError(`找不到待補的問題 id：${input.id}`);
      return { resolved: rows[0] };
    },

    /** 跨專案列出現行規則，給前端的規則頁和待確認清單用。 */
    async listRules(input: ListRulesInput) {
      const params: unknown[] = [];
      const add = (value: unknown) => `$${params.push(value)}`;
      const where = ['r.retired_at IS NULL', `p.owner_id = ${add(ownerId)}`];
      if (input.project) where.push(`lower(p.name) = lower(${add(input.project.trim())})`);
      if (input.module) where.push(`lower(m.name) = lower(${add(input.module.trim())})`);
      if (input.confidence) where.push(`r.confidence = ${add(input.confidence)}`);
      for (const term of searchTerms(input.query)) where.push(`(r.title || ' ' || r.body) ILIKE ${add(likePattern(term))}`);

      const { rows } = await db.query(
        `SELECT r.id, p.name AS project, m.name AS module, r.title, r.body,
                r.confidence, r.confirmed_by, r.updated_at
         FROM rules r
         JOIN modules m ON m.id = r.module_id
         JOIN projects p ON p.id = m.project_id
         WHERE ${where.join(' AND ')}
         ORDER BY p.name, m.name, r.title`,
        params,
      );
      return { count: rows.length, rules: rows };
    },
  };
}

export type Notes = ReturnType<typeof createNotes>;

/**
 * 觀念卡屬於專案 p：指定了所屬專案，或連到 p 的改動紀錄。
 * 用在 SQL 裡，前提是查詢中有別名為 c 的 concepts 和別名為 p 的 projects。
 */
export const CONCEPT_IN_PROJECT = `c.owner_id = p.owner_id AND (c.project_id = p.id OR EXISTS (
  SELECT 1 FROM entry_concepts ec JOIN entries e ON e.id = ec.entry_id
  WHERE ec.concept_id = c.id AND e.project_id = p.id))`;

/** 舊筆記可能早於機密檢查，或當時沒打算公開，所以公開前再掃一次整個專案。 */
async function assertPublishable(q: Queryable, projectId: number) {
  const entries = await q.query<{ label: string; text: string }>(
    `SELECT '紀錄「' || title || '」' AS label, search_text AS text FROM entries WHERE project_id = $1`,
    [projectId],
  );
  const rules = await q.query<{ label: string; text: string }>(
    `SELECT '規則「' || r.title || '」' AS label, r.title || ' ' || r.body AS text
     FROM rules r JOIN modules m ON m.id = r.module_id
     WHERE m.project_id = $1 AND r.retired_at IS NULL`,
    [projectId],
  );
  const concepts = await q.query<{ label: string; text: string }>(
    `SELECT '觀念「' || c.term || '」' AS label, c.search_text AS text
     FROM concepts c JOIN projects p ON p.id = $1
     WHERE ${CONCEPT_IN_PROJECT}`,
    [projectId],
  );
  const articles = await q.query<{ label: string; text: string }>(
    `SELECT '筆記「' || title || '」' AS label, search_text AS text FROM articles WHERE project_id = $1`,
    [projectId],
  );
  const flagged = [...entries.rows, ...rules.rows, ...concepts.rows, ...articles.rows].filter((row) => {
    try {
      assertNoSecrets(row.text);
      return false;
    } catch (err) {
      if (err instanceof NotesError) return true;
      throw err;
    }
  });
  if (flagged.length > 0) {
    throw new NotesError(`這些內容疑似有 email、密碼或 token，不適合公開，請先修改：${flagged.map((row) => row.label).join('、')}`);
  }
}

/** 所有登入者都看得到的公開專案，只讀 sharing = 'public' 的內容。 */
export async function listPublicNotes(db: Database, input: { project?: string }) {
  const projects = await db.query<{ name: string; owner: string; entry_count: number; rule_count: number }>(
    `SELECT p.name, CASE WHEN u.name LIKE '%@%' THEN '匿名使用者' ELSE u.name END AS owner,
            (SELECT count(*)::int FROM entries e WHERE e.project_id = p.id) AS entry_count,
            (SELECT count(*)::int FROM rules r JOIN modules m ON m.id = r.module_id
             WHERE m.project_id = p.id AND r.retired_at IS NULL) AS rule_count
     FROM projects p JOIN users u ON u.id = p.owner_id
     WHERE p.sharing = 'public' ORDER BY p.name`,
  );
  const name = input.project?.trim();
  if (!name) return { projects: projects.rows, entries: [], rules: [] };
  const entries = await db.query(
    `SELECT e.id, m.name AS module, e.title, e.what_changed, e.why, e.how_to_explain, e.happened_on::text
     FROM entries e JOIN projects p ON p.id = e.project_id LEFT JOIN modules m ON m.id = e.module_id
     WHERE p.sharing = 'public' AND lower(p.name) = lower($1)
     ORDER BY e.happened_on DESC, e.id DESC LIMIT 50`,
    [name],
  );
  const rules = await db.query(
    `SELECT m.name AS module, r.title, r.body, r.confidence
     FROM rules r JOIN modules m ON m.id = r.module_id JOIN projects p ON p.id = m.project_id
     WHERE p.sharing = 'public' AND lower(p.name) = lower($1) AND r.retired_at IS NULL
     ORDER BY m.name, r.title LIMIT 200`,
    [name],
  );
  return { projects: projects.rows, entries: entries.rows, rules: rules.rows };
}
