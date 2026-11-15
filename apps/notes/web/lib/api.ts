import 'server-only';
import { cookies } from 'next/headers';
import { SESSION_COOKIE, verifySession } from '@/lib/session';
import type { McpToken } from '@/lib/types';

export type { McpToken };

export type Confidence = 'general_definition' | 'code_inferred' | 'colleague_confirmed';
export type TestStatus = 'not_tested' | 'manual' | 'automated' | 'production_verified';

export interface Project {
  name: string;
  modules: string[];
  entry_count: number;
  concept_count: number;
  rule_count: number;
  article_count: number;
}

export interface Article {
  id: number;
  project: string;
  module: string | null;
  title: string;
  body: string;
  open_questions: string | null;
  confidence: Confidence;
  confirmed_by: string | null;
  created_at: string;
  updated_at: string;
  concepts: string[];
}

export interface Entry {
  id: number;
  project: string;
  module: string | null;
  title: string;
  what_changed: string;
  why: string;
  logic_notes: string | null;
  data_notes: string | null;
  test_status: TestStatus;
  test_notes: string | null;
  how_to_explain: string | null;
  pr_url: string | null;
  happened_on: string;
  concepts: string[];
}

export interface Concept {
  id: number;
  term: string;
  plain_explanation: string;
  why_needed: string | null;
  example: string | null;
  where_in_system: string | null;
  open_questions: string | null;
  confidence: Confidence;
  confirmed_by: string | null;
  related: string[];
  entry_ids: number[];
  project?: string | null;
}

export interface Rule {
  id: number;
  project: string;
  module: string;
  title: string;
  body: string;
  confidence: Confidence;
  confirmed_by: string | null;
  updated_at: string;
}

type Params = Record<string, string | number | boolean | undefined>;

/** 使用者身分只取自簽章過的 cookie，所以每個人只查得到自己的筆記。 */
async function userHeaders(): Promise<Record<string, string>> {
  const session = (await cookies()).get(SESSION_COOKIE)?.value;
  const userId = await verifySession(session, process.env.SESSION_SECRET);
  if (userId === null) throw new Error('沒有登入');
  return { Authorization: `Bearer ${process.env.API_TOKEN}`, 'X-Notes-User': String(userId) };
}

/** token 只在伺服器端使用，不會送到瀏覽器。 */
async function get<T>(path: string, params: Params = {}): Promise<T> {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') query.set(key, String(value));
  }
  const res = await fetch(`${process.env.API_URL}${path}?${query}`, {
    headers: await userHeaders(),
    cache: 'no-store',
  });
  if (!res.ok) throw new Error(`API ${path} 回應 ${res.status}`);
  return (await res.json()) as T;
}

/** 寫入類的呼叫，把後端的狀態碼和錯誤訊息原樣交給呼叫端。 */
async function send(path: string, init: RequestInit) {
  const res = await fetch(`${process.env.API_URL}${path}`, {
    ...init,
    headers: { ...(await userHeaders()), 'content-type': 'application/json' },
    cache: 'no-store',
  });
  return { status: res.status, body: (await res.json()) as unknown };
}

/** 通過 RAG 登入後取得筆記的使用者。這時還沒有 cookie，所以只帶 API token。 */
export async function upsertLoginUser(ragUserId: number, name: string, refreshToken?: string): Promise<{ id: number; name: string }> {
  const res = await fetch(`${process.env.API_URL}/api/users/login`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${process.env.API_TOKEN}`, 'content-type': 'application/json' },
    body: JSON.stringify({ rag_user_id: ragUserId, name, rag_refresh_token: refreshToken }),
    cache: 'no-store',
  });
  if (!res.ok) throw new Error(`API /api/users/login 回應 ${res.status}`);
  return (await res.json()) as { id: number; name: string };
}

export type Sharing = 'off' | 'private' | 'public';
export interface SharingProject {
  name: string;
  sharing: Sharing;
  entry_count: number;
  rule_count: number;
  concept_count: number;
  article_count: number;
}
export interface SyncStatus {
  linked: boolean;
  synced_at: string | null;
  error: string | null;
}
export interface PublicProject {
  name: string;
  owner: string;
  entry_count: number;
  rule_count: number;
}
export interface PublicEntry {
  id: number;
  module: string | null;
  title: string;
  what_changed: string;
  why: string;
  how_to_explain: string | null;
  happened_on: string;
}
export interface PublicRule {
  module: string;
  title: string;
  body: string;
  confidence: Confidence;
}
export interface KnowledgeGap {
  id: number;
  question: string;
  created_at: string;
}

/** 自己的全部筆記，原樣交給下載用的路由 */
export const exportNotes = () => get<unknown>('/api/export');
export const getSharing = () => get<{ projects: SharingProject[]; unassigned_concepts: number; status: SyncStatus }>('/api/sharing');
export const setSharing = (project: string, sharing: Sharing) => send('/api/sharing', { method: 'PUT', body: JSON.stringify({ project, sharing }) });
export const listPublic = (project?: string) => get<{ projects: PublicProject[]; entries: PublicEntry[]; rules: PublicRule[] }>('/api/public', { project });
export const listGaps = () => get<{ count: number; gaps: KnowledgeGap[] }>('/api/gaps');
export const addGap = (question: string) => send('/api/gaps', { method: 'POST', body: JSON.stringify({ question }) });
export const resolveGap = (id: string) => send(`/api/gaps/${id}/resolve`, { method: 'POST', body: '{}' });

export const listMcpTokens = () => get<{ tokens: McpToken[] }>('/api/tokens');
export const createMcpToken = (name: string) => send('/api/tokens', { method: 'POST', body: JSON.stringify({ name }) });
export const revokeMcpToken = (id: string) => send(`/api/tokens/${id}`, { method: 'DELETE' });

export const PAGE_SIZE = 20;

export interface Stats {
  entries: number;
  articles: number;
  concepts: number;
  rules: number;
  open_questions: number;
  general_definition: number;
  code_inferred: number;
  colleague_confirmed: number;
}

export const getStats = () => get<Stats>('/api/stats');
export const listProjects = () => get<{ projects: Project[] }>('/api/projects');
export const listEntries = (params: Params) => get<{ count: number; entries: Entry[] }>('/api/entries', { limit: PAGE_SIZE, ...params });
export const listConcepts = (params: Params) => get<{ count: number; concepts: Concept[] }>('/api/concepts', { limit: PAGE_SIZE, ...params });
export const listRules = (params: Params) => get<{ count: number; rules: Rule[] }>('/api/rules', params);
export const listArticles = (params: Params) => get<{ count: number; articles: Article[] }>('/api/articles', { limit: PAGE_SIZE, ...params });
