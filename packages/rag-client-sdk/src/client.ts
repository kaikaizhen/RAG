import type { components } from './generated/schema.js';
import { RagApiError, RagUnauthorizedError, RagValidationError, toRagError } from './errors.js';
import { MemoryTokenStore, type StoredSession, type TokenStore } from './tokenStore.js';

type Schemas = components['schemas'];

export type LoginResult = Schemas['LoginVM'];
export type Space = Schemas['SpaceVM'];
export type CurrentSpace = Schemas['CurrentSpaceVM'];
export type EffectiveSkills = Schemas['EffectiveSkillVM'];
export type ChatAnswer = Schemas['ChatVM'];
export type ChatSummary = Schemas['GetChatHistoryVM'];
export type ChatLogEntry = Schemas['GetChatLogHistoryVM'];

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
  };

  readonly chat = {
    send: (question: string, options: { chatId?: string; type?: string } = {}) =>
      this.request<ChatAnswer>('POST', '/Chat', { body: { question, chatID: options.chatId ?? null, type: options.type ?? null } }),
    history: () => this.request<ChatSummary[]>('GET', '/Chat'),
    logs: (chatId: string) => this.request<ChatLogEntry[]>('GET', `/Chat/${encodeURIComponent(chatId)}`),
    delete: (chatId: string) => this.request<string>('DELETE', `/Chat/${encodeURIComponent(chatId)}`),
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

  private async request<T>(method: string, path: string, options: RequestOptions = {}, isRetry = false): Promise<T> {
    const authenticated = options.authenticated ?? true;
    const headers: Record<string, string> = { Accept: 'application/json' };
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
    });

    if (response.status === 401 && authenticated) {
      if (!isRetry && (await this.refreshSession())) return this.request<T>(method, path, options, true);
      await this.expireSession();
      throw toRagError(401, await readBody(response), 'Unauthorized');
    }

    const body = await readBody(response);
    if (!response.ok) throw toRagError(response.status, body, response.statusText || `HTTP ${response.status}`);
    return body as T;
  }
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
