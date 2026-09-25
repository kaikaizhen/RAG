/**
 * API 錯誤：呼叫端依類別區分 401 / 403 / 404 等，不需自行解析 status code。
 */
export class RagApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.name = 'RagApiError';
    this.status = status;
    this.body = body;
  }
}

/** 400：參數或 Space selection 格式錯誤 */
export class RagValidationError extends RagApiError {
  constructor(message: string, body?: unknown) {
    super(400, message, body);
    this.name = 'RagValidationError';
  }
}

/** 401：未登入或 session 已失效（SDK 已清除本機 session） */
export class RagUnauthorizedError extends RagApiError {
  constructor(message: string, body?: unknown) {
    super(401, message, body);
    this.name = 'RagUnauthorizedError';
  }
}

/** 403：已登入但無權使用所選 Space 或資源（選擇 Space 不代表已授權） */
export class RagForbiddenError extends RagApiError {
  constructor(message: string, body?: unknown) {
    super(403, message, body);
    this.name = 'RagForbiddenError';
  }
}

/** 404：資源不存在或不屬於目前使用者 / Space（兩者刻意不區分） */
export class RagNotFoundError extends RagApiError {
  constructor(message: string, body?: unknown) {
    super(404, message, body);
    this.name = 'RagNotFoundError';
  }
}

/** 409：與目前資料狀態衝突 */
export class RagConflictError extends RagApiError {
  constructor(message: string, body?: unknown) {
    super(409, message, body);
    this.name = 'RagConflictError';
  }
}

/** 503：功能尚未在伺服器啟用（例如 Skill Runtime 未設定） */
export class RagServiceUnavailableError extends RagApiError {
  constructor(message: string, body?: unknown) {
    super(503, message, body);
    this.name = 'RagServiceUnavailableError';
  }
}

export function toRagError(status: number, body: unknown, fallback: string): RagApiError {
  const message = extractMessage(body) ?? fallback;
  switch (status) {
    case 400: return new RagValidationError(message, body);
    case 401: return new RagUnauthorizedError(message, body);
    case 403: return new RagForbiddenError(message, body);
    case 404: return new RagNotFoundError(message, body);
    case 409: return new RagConflictError(message, body);
    case 503: return new RagServiceUnavailableError(message, body);
    default: return new RagApiError(status, message, body);
  }
}

function extractMessage(body: unknown): string | undefined {
  if (typeof body === 'string' && body.length > 0) return body;
  if (body && typeof body === 'object') {
    const record = body as Record<string, unknown>;
    if (typeof record.error === 'string') return record.error;
    if (typeof record.title === 'string') return record.title;
  }
  return undefined;
}
