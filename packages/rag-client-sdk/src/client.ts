import type { components } from './generated/schema.js';
import { RagApiError, RagUnauthorizedError, RagValidationError, toRagError } from './errors.js';
import { MemoryTokenStore, type StoredSession, type TokenStore } from './tokenStore.js';
import { QuestionQueryType, type MutationResult, type QaType, type QaTypeItem, type Question, type User } from './models.js';

type Schemas = components['schemas'];

export type LoginResult = Schemas['LoginVM'];
export type Space = Schemas['SpaceVM'];
export type CurrentSpace = Schemas['CurrentSpaceVM'];
export type EffectiveSkills = Schemas['EffectiveSkillVM'];
export type ChatAnswer = Schemas['ChatVM'];
export type ChatSummary = Schemas['GetChatHistoryVM'];
export type ChatLogEntry = Schemas['GetChatLogHistoryVM'];
export type SkillBundle = Schemas['SkillBundleVM'];
export type KnowledgeSearchResult = Schemas['KnowledgeSearchVM'];
export type KnowledgeItem = Schemas['KnowledgeItemVM'];

export type RegisterInput = Schemas['RegisterParam'];
export type RegistrationResult = Schemas['RegistrationResultVM'];
export type CreateUserInput = Schemas['CreateUserParam'];
export type UpdateUserInput = Schemas['UpdateUserParam'];
export type CreateQaTypeInput = Schemas['CreateQaTypeParam'];
export type UpdateQaTypeInput = Schemas['UpdateQaTypeParam'];
export type CreateQaTypeItemInput = Schemas['CreateQaTypeItemParam'];
export type UpdateQaTypeItemInput = Schemas['UpdateQaTypeItemParam'];
export type CreateQuestionInput = Schemas['CreateQuestionParam'];
export type UpdateQuestionInput = Schemas['UpdateQuestionParam'];

/** ChatStream 事件：chat = 這次問答所屬的聊天室編號（先於文字送出）；text = 一段回答文字 */
export type ChatStreamEvent = { type: 'chat'; chatId: string } | { type: 'text'; text: string };

export const API_VERSION = 'v1';
export const SPACE_HINT_HEADER = 'X-Space-Id';

export interface RagClientOptions {
  /** .NET API 位址（例如 https://rag.example.com）；SDK 只會呼叫 /api/v1/* */
  baseUrl: string;
  tokenStore?: TokenStore;
  fetch?: typeof fetch;
}

export interface RagClientEvents {
  /** 登入、refresh 或登出後 session 改變（null = 已登出） */
  'session-changed': { userId: number } | null;
  /** refresh 失敗，本機 session 已清除，需要重新登入 */
  'session-expired': undefined;
  /** Space selection hint 改變；已清除所有 Space 相關快取 */
  'space-changed': { spaceId: number | null };
}

type Listener<T> = (payload: T) => void;

interface RequestOptions {
  body?: unknown;
  accept?: string;
  signal?: AbortSignal;
  /** 是否附加 Authorization / Space hint 並在 401 時嘗試 refresh */
  authenticated?: boolean;
}

/**
 * RAG API client。授權一律由伺服器決定：Space selection 只是 hint，SDK 不保存任何授權結果、
 * 不建構 RAG scope 或 capability 清單，也不直接連線 RAG Server / Qdrant。
 */
export class RagClient {
  private readonly baseUrl: string;
  private readonly tokenStore: TokenStore;
  private readonly fetchImpl: typeof fetch;
  private readonly listeners = new Map<keyof RagClientEvents, Set<Listener<never>>>();
  private readonly cache = new Map<string, Promise<unknown>>();
  private selectedSpaceId: number | null = null;
  private refreshing: Promise<boolean> | null = null;

  readonly auth = {
    login: (account: string, password: string) => this.login(account, password),
    /**
     * 自行註冊（不需登入）：帳號為信箱，伺服器寄出驗證信。不論信箱是否已註冊回應都相同；
     * 帳號在使用者點擊信中連結後才建立，並取得自己的個人 Space。伺服器未開放註冊時為 503。
     */
    register: (input: RegisterInput) =>
      this.request<RegistrationResult>('POST', '/Auth/Register', { body: input, authenticated: false }),
    /** 驗證信中連結附帶的 token；重複驗證同一個連結視為成功 */
    verifyEmail: (token: string) =>
      this.request<RegistrationResult>('POST', '/Auth/VerifyEmail', { body: { token }, authenticated: false }),
    refresh: () => this.refreshSession(),
    logout: () => this.logout(),
    session: () => this.tokenStore.get(),
  };

  readonly spaces = {
    /** 目前帳號可使用的 Space（伺服器依 membership 決定） */
    list: () => this.cached('spaces', () => this.request<Space[]>('GET', '/Space')),
    /** 伺服器依 selection hint 解析出的 Space；未授權時拋出 RagForbiddenError */
    current: () => this.cached(`space:${this.selectedSpaceId ?? 'auto'}:current`, () => this.request<CurrentSpace>('GET', '/Space/current')),
    /** 設定 Space selection hint（null = 交由伺服器依預設規則選擇）；不代表已授權 */
    select: (spaceId: number | null) => this.selectSpace(spaceId),
    selected: () => this.selectedSpaceId,
  };

  readonly skills = {
    /** 目前 Space 的有效 Skill / Workflow metadata（不含內容） */
    effective: () => this.cached(`space:${this.selectedSpaceId ?? 'auto'}:skills`, () => this.request<EffectiveSkills>('GET', '/Skill/effective')),
    /** 目前 Space 的有效 Skill / Workflow 內容，受 token 預算限制（未指定時使用伺服器預設） */
    bundle: (tokenBudget?: number) => {
      if (tokenBudget !== undefined && !(Number.isInteger(tokenBudget) && tokenBudget > 0)) {
        return Promise.reject(new RagValidationError('tokenBudget must be a positive integer'));
      }
      const query = tokenBudget === undefined ? '' : `?tokenBudget=${tokenBudget}`;
      return this.cached(`space:${this.selectedSpaceId ?? 'auto'}:bundle:${tokenBudget ?? 'default'}`, () => this.request<SkillBundle>('GET', `/Skill/bundle${query}`));
    },
  };

  readonly knowledge = {
    /**
     * 在目前 Space 的知識庫檢索（只檢索、不產生答案）。
     * status：grounded（有 Verified 證據）/ partial（僅 Stale、Inferred）/ insufficient（無證據）；similar 不可當作依據。
     */
    search: (query: string, options: { type?: string } = {}) =>
      this.request<KnowledgeSearchResult>('POST', '/Knowledge/search', { body: { query, type: options.type ?? null } }),
  };

  readonly chat = {
    send: (question: string, options: { chatId?: string; type?: string } = {}) =>
      this.request<ChatAnswer>('POST', '/Chat', { body: { question, chatID: options.chatId ?? null, type: options.type ?? null } }),
    history: () => this.request<ChatSummary[]>('GET', '/Chat'),
    logs: (chatId: string) => this.request<ChatLogEntry[]>('GET', `/Chat/${encodeURIComponent(chatId)}`),
    delete: (chatId: string) => this.request<string>('DELETE', `/Chat/${encodeURIComponent(chatId)}`),
    /**
     * 串流取得回答（POST /Chat/ChatStream，SSE）。先 yield 一次 `chat` 事件（聊天室編號，新對話時即剛建立的編號），
     * 之後 yield `text` 事件，串接即為完整答案。伺服器逐字送出 `data: <字元>`；空的 data 行代表換行字元。
     * 傳入 signal 可中止。
     */
    stream: (question: string, options: { chatId?: string; type?: string; signal?: AbortSignal } = {}) =>
      this.streamChat(question, options),
  };

  readonly qaTypes = {
    list: () => this.request<QaType[]>('GET', '/QaType'),
    create: (input: CreateQaTypeInput) => this.request<MutationResult>('POST', '/QaType', { body: input }),
    update: (input: UpdateQaTypeInput) => this.request<MutationResult>('PUT', '/QaType', { body: input }),
    delete: (id: number) => this.request<MutationResult>('DELETE', `/QaType/${pathId(id)}`),
  };

  readonly qaTypeItems = {
    list: () => this.request<QaTypeItem[]>('GET', '/QaTypeItem'),
    create: (input: CreateQaTypeItemInput) => this.request<MutationResult>('POST', '/QaTypeItem', { body: input }),
    update: (input: UpdateQaTypeItemInput) => this.request<MutationResult>('PUT', '/QaTypeItem', { body: input }),
    delete: (id: number) => this.request<MutationResult>('DELETE', `/QaTypeItem/${pathId(id)}`),
  };

  /** 目前 Space 的問答知識庫；範圍由伺服器依 Space 決定，不需（也不能）帶 UserID */
  readonly questions = {
    /** 不帶參數 = 全部；帶 qaTypeId = 依種類查詢 */
    list: (options: { qaTypeId?: number } = {}) => {
      const query = options.qaTypeId === undefined ? `?Type=${QuestionQueryType.All}` : `?Type=${QuestionQueryType.Type}&Id=${pathId(options.qaTypeId)}`;
      return this.request<Question[]>('GET', `/Question${query}`);
    },
    create: (input: CreateQuestionInput) => this.request<MutationResult>('POST', '/Question', { body: input }),
    update: (input: UpdateQuestionInput) => this.request<MutationResult>('PUT', '/Question', { body: input }),
    delete: (id: number) => this.request<MutationResult>('DELETE', `/Question/${pathId(id)}`),
  };

  readonly users = {
    get: (id: number) => this.request<User>('GET', `/User/${pathId(id)}`),
    /** 建立帳號；後端要求已登入（不開放匿名自行註冊），新帳號的 manage 一律為 free */
    create: (input: CreateUserInput) => this.request<MutationResult>('POST', '/User', { body: input }),
    update: (input: UpdateUserInput) => this.request<MutationResult>('PUT', '/User', { body: input }),
    delete: (id: number) => this.request<MutationResult>('DELETE', `/User/${pathId(id)}`),
  };

  constructor(options: RagClientOptions) {
    if (!options.baseUrl) throw new Error('baseUrl is required');
    this.baseUrl = options.baseUrl.replace(/\/+$/, '');
    this.tokenStore = options.tokenStore ?? new MemoryTokenStore();
    const fetchImpl = options.fetch ?? globalThis.fetch;
    if (!fetchImpl) throw new Error('fetch is not available; pass options.fetch');
    this.fetchImpl = fetchImpl.bind(globalThis);
  }

  on<K extends keyof RagClientEvents>(event: K, listener: Listener<RagClientEvents[K]>): () => void {
    let set = this.listeners.get(event);
    if (!set) {
      set = new Set();
      this.listeners.set(event, set);
    }
    set.add(listener as Listener<never>);
    return () => set!.delete(listener as Listener<never>);
  }

  private emit<K extends keyof RagClientEvents>(event: K, payload: RagClientEvents[K]): void {
    for (const listener of this.listeners.get(event) ?? []) (listener as Listener<RagClientEvents[K]>)(payload);
  }

  // ---------- session ----------

  private async login(account: string, password: string): Promise<LoginResult> {
    const result = await this.request<LoginResult>('POST', '/Auth/Login', { body: { account, password }, authenticated: false });
    await this.storeSession(result);
    return result;
  }

  private async refreshSession(): Promise<boolean> {
    // 同一時間只發出一次 refresh，其餘請求共用結果
    this.refreshing ??= (async () => {
      const current = await this.tokenStore.get();
      if (!current?.refreshToken) return false;
      try {
        const result = await this.request<LoginResult>('POST', '/Auth/Refresh', { body: { refreshToken: current.refreshToken }, authenticated: false });
        await this.storeSession(result);
        return true;
      } catch (error) {
        if (error instanceof RagApiError && error.status === 401) return false;
        throw error;
      }
    })().finally(() => {
      this.refreshing = null;
    });
    return this.refreshing;
  }

  private async logout(): Promise<void> {
    await this.clearLocalState();
    this.emit('session-changed', null);
  }

  private async storeSession(result: LoginResult): Promise<void> {
    if (typeof result.userID !== 'number' || !result.accessToken || !result.refreshToken) {
      throw new RagApiError(500, 'Invalid session response', result);
    }
    const session: StoredSession = { userId: result.userID, accessToken: result.accessToken, refreshToken: result.refreshToken };
    const previous = await this.tokenStore.get();
    await this.tokenStore.set(session);
    if (previous?.userId !== session.userId) {
      this.selectedSpaceId = null;
      this.cache.clear();
    }
    this.emit('session-changed', { userId: session.userId });
  }

  private async expireSession(): Promise<void> {
    await this.clearLocalState();
    this.emit('session-expired', undefined);
  }

  private async clearLocalState(): Promise<void> {
    await this.tokenStore.clear();
    this.cache.clear();
    this.selectedSpaceId = null;
  }

  // ---------- space ----------

  private selectSpace(spaceId: number | null): void {
    if (spaceId !== null && !(Number.isInteger(spaceId) && spaceId > 0)) {
      throw new RagValidationError('spaceId must be a positive integer or null');
    }
    if (spaceId === this.selectedSpaceId) return;
    this.selectedSpaceId = spaceId;
    this.cache.clear();
    this.emit('space-changed', { spaceId });
  }

  private cached<T>(key: string, load: () => Promise<T>): Promise<T> {
    const existing = this.cache.get(key);
    if (existing) return existing as Promise<T>;
    const pending = load().catch((error: unknown) => {
      this.cache.delete(key);
      throw error;
    });
    this.cache.set(key, pending);
    return pending;
  }

  // ---------- transport ----------

  private async request<T>(method: string, path: string, options: RequestOptions = {}): Promise<T> {
    const response = await this.send(method, path, options);
    const body = await readBody(response);
    if (!response.ok) throw toRagError(response.status, body, response.statusText || `HTTP ${response.status}`);
    return body as T;
  }

  /** 送出請求並處理 401 refresh；回傳尚未讀取 body 的 Response（非 401 的錯誤狀態由呼叫端處理） */
  private async send(method: string, path: string, options: RequestOptions = {}, isRetry = false): Promise<Response> {
    const authenticated = options.authenticated ?? true;
    const headers: Record<string, string> = { Accept: options.accept ?? 'application/json' };
    if (options.body !== undefined) headers['Content-Type'] = 'application/json';

    if (authenticated) {
      const session = await this.tokenStore.get();
      if (!session) throw new RagUnauthorizedError('Not signed in');
      headers.Authorization = `Bearer ${session.accessToken}`;
      if (this.selectedSpaceId !== null) headers[SPACE_HINT_HEADER] = String(this.selectedSpaceId);
    }

    const response = await this.fetchImpl(`${this.baseUrl}/api/${API_VERSION}${path}`, {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    });

    if (response.status === 401 && authenticated) {
      if (!isRetry && (await this.refreshSession())) return this.send(method, path, options, true);
      await this.expireSession();
      throw toRagError(401, await readBody(response), 'Unauthorized');
    }
    return response;
  }

  private async *streamChat(question: string, options: { chatId?: string; type?: string; signal?: AbortSignal }): AsyncGenerator<ChatStreamEvent, void, void> {
    const response = await this.send('POST', '/Chat/ChatStream', {
      body: { question, chatID: options.chatId ?? null, type: options.type ?? null },
      accept: 'text/event-stream',
      signal: options.signal,
    });
    if (!response.ok) {
      const body = await readBody(response);
      throw toRagError(response.status, body, response.statusText || `HTTP ${response.status}`);
    }
    if (!response.body) return;

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    try {
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        let boundary: number;
        // 事件以空行分隔；空 data 事件的前後會多出 \n，統一在事件之間略過
        while ((boundary = buffer.indexOf('\n\n')) !== -1) {
          const block = buffer.slice(0, boundary);
          buffer = buffer.slice(boundary + 2);
          const event = parseSseEvent(block);
          if (event) yield event;
        }
        buffer = buffer.replace(/^\n+/, '');
      }
      const tail = parseSseEvent(buffer);
      if (tail) yield tail;
    } finally {
      reader.releaseLock();
    }
  }
}

function pathId(id: number): number {
  if (!Number.isInteger(id) || id <= 0) throw new RagValidationError('id must be a positive integer');
  return id;
}

/** 解析一個 SSE 區塊；空 data = 換行字元；沒有 data 欄位回傳 null */
function parseSseEvent(block: string): ChatStreamEvent | null {
  const lines = block.replace(/^\n+/, '').split('\n');
  let name = 'message';
  let data: string | null = null;
  for (const line of lines) {
    if (line.startsWith('event:')) name = line.slice('event:'.length).trim();
    else if (line.startsWith('data:')) data = line.slice('data:'.length).replace(/^ /, '');
  }
  if (data === null) return null;
  if (name === 'chat') return { type: 'chat', chatId: data.trim() };
  return { type: 'text', text: data === '' ? '\n' : data };
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return undefined;
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('json')) {
    try {
      return JSON.parse(text) as unknown;
    } catch {
      return text;
    }
  }
  return text;
}
