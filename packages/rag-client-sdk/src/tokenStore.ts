/**
 * 保存於本機的 session。只保存 token 與使用者編號，不保存任何授權結果或 Space 權限。
 */
export interface StoredSession {
  userId: number;
  accessToken: string;
  refreshToken: string;
}

/**
 * Token 儲存抽象：CLI 應以 OS credential store、VS Code Extension 應以 SecretStorage 實作；
 * 不可使用明文設定檔或瀏覽器 localStorage 保存。
 */
export interface TokenStore {
  get(): Promise<StoredSession | null>;
  set(session: StoredSession): Promise<void>;
  clear(): Promise<void>;
}

/** 僅存在記憶體（預設；程序結束即消失） */
export class MemoryTokenStore implements TokenStore {
  private session: StoredSession | null = null;

  async get(): Promise<StoredSession | null> {
    return this.session;
  }

  async set(session: StoredSession): Promise<void> {
    this.session = { ...session };
  }

  async clear(): Promise<void> {
    this.session = null;
  }
}
