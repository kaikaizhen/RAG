import { createCipheriv, createDecipheriv, createHash, randomBytes } from 'node:crypto';
import type { Database } from './db.js';
import { NotesError } from './errors.js';

const MAX_ACTIVE_TOKENS = 10;

function hashToken(token: string): string {
  return createHash('sha256').update(token).digest('hex');
}

function encrypt(plain: string, secret: string): string {
  const iv = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', createHash('sha256').update(secret).digest(), iv);
  const data = Buffer.concat([cipher.update(plain, 'utf8'), cipher.final()]);
  return [iv, cipher.getAuthTag(), data].map((part) => part.toString('base64url')).join('.');
}

function decrypt(value: string, secret: string): string {
  const [iv, tag, data] = value.split('.').map((part) => Buffer.from(part, 'base64url'));
  const decipher = createDecipheriv('aes-256-gcm', createHash('sha256').update(secret).digest(), iv!);
  decipher.setAuthTag(tag!);
  return Buffer.concat([decipher.update(data!), decipher.final()]).toString('utf8');
}

export function createUsers(db: Database, ownerRagUserId?: number, syncKey?: string) {
  return {
    /** 只有設定了 SYNC_KEY 才會留下 token；沒有金鑰就不存，背景同步也不會運作。 */
    async saveRefreshToken(userId: number, token: string) {
      if (!syncKey) return;
      await db.query('UPDATE users SET rag_refresh_enc = $2, rag_sync_error = NULL WHERE id = $1', [userId, encrypt(token, syncKey)]);
    },

    async refreshTokenFor(userId: number): Promise<string | null> {
      if (!syncKey) return null;
      const { rows } = await db.query<{ rag_refresh_enc: string | null }>('SELECT rag_refresh_enc FROM users WHERE id = $1', [userId]);
      const stored = rows[0]?.rag_refresh_enc;
      if (!stored) return null;
      try {
        return decrypt(stored, syncKey);
      } catch {
        return null; // 換過 SYNC_KEY，舊的解不開，等使用者重新登入
      }
    },

    async clearRefreshToken(userId: number) {
      await db.query('UPDATE users SET rag_refresh_enc = NULL WHERE id = $1', [userId]);
    },

    async recordSync(userId: number, error: string | null) {
      await db.query(
        `UPDATE users SET rag_sync_error = $2, rag_synced_at = CASE WHEN $2::text IS NULL THEN now() ELSE rag_synced_at END WHERE id = $1`,
        [userId, error],
      );
    },

    async linkedUserIds(): Promise<number[]> {
      const { rows } = await db.query<{ id: number }>('SELECT id FROM users WHERE rag_refresh_enc IS NOT NULL');
      return rows.map((row) => row.id);
    },

    async syncStatus(userId: number) {
      const { rows } = await db.query<{ linked: boolean; synced_at: string | null; error: string | null }>(
        'SELECT rag_refresh_enc IS NOT NULL AS linked, rag_synced_at AS synced_at, rag_sync_error AS error FROM users WHERE id = $1',
        [userId],
      );
      return rows[0]!;
    },

    async originalUserId(): Promise<number> {
      const { rows } = await db.query<{ id: number }>('SELECT id FROM users WHERE is_original');
      return rows[0]!.id;
    },

    async exists(id: number): Promise<boolean> {
      const { rows } = await db.query('SELECT 1 FROM users WHERE id = $1', [id]);
      return rows.length > 0;
    },

    /** 通過 RAG 登入後呼叫：第一次見到的帳號會建立，原始擁有者會接手多人化之前的筆記。 */
    async login(input: { ragUserId: number; name: string }) {
      return db.tx(async (q) => {
        if (input.ragUserId === ownerRagUserId) {
          await q.query(
            `UPDATE users SET rag_user_id = $1, name = $2
             WHERE is_original AND rag_user_id IS NULL
               AND NOT EXISTS (SELECT 1 FROM users WHERE rag_user_id = $1)`,
            [input.ragUserId, input.name],
          );
        }
        const { rows } = await q.query<{ id: number; name: string }>(
          `INSERT INTO users (rag_user_id, name) VALUES ($1, $2)
           ON CONFLICT (rag_user_id) DO UPDATE SET name = EXCLUDED.name
           RETURNING id, name`,
          [input.ragUserId, input.name],
        );
        return rows[0]!;
      });
    },

    /** token 有效時回傳使用者 id，順便記下最後使用時間。 */
    async userForToken(token: string): Promise<number | null> {
      const { rows } = await db.query<{ user_id: number }>(
        `UPDATE mcp_tokens SET last_used_at = now()
         WHERE token_hash = $1 AND revoked_at IS NULL
         RETURNING user_id`,
        [hashToken(token)],
      );
      return rows[0]?.user_id ?? null;
    },

    async listTokens(userId: number) {
      const { rows } = await db.query(
        `SELECT id, name, created_at, last_used_at FROM mcp_tokens
         WHERE user_id = $1 AND revoked_at IS NULL ORDER BY created_at DESC, id DESC`,
        [userId],
      );
      return { tokens: rows };
    },

    /** 明文只在這裡回傳一次，資料庫只留雜湊。 */
    async createToken(userId: number, name: string) {
      const token = `ln_${randomBytes(32).toString('base64url')}`;
      return db.tx(async (q) => {
        const active = await q.query<{ count: number }>(
          'SELECT count(*)::int AS count FROM mcp_tokens WHERE user_id = $1 AND revoked_at IS NULL',
          [userId],
        );
        if (active.rows[0]!.count >= MAX_ACTIVE_TOKENS) {
          throw new NotesError(`最多只能有 ${MAX_ACTIVE_TOKENS} 組 token，請先撤銷用不到的`);
        }
        const { rows } = await q.query<{ id: number; name: string }>(
          'INSERT INTO mcp_tokens (user_id, name, token_hash) VALUES ($1, $2, $3) RETURNING id, name',
          [userId, name.trim(), hashToken(token)],
        );
        return { ...rows[0]!, token };
      });
    },

    async revokeToken(userId: number, id: number) {
      const { rows } = await db.query<{ id: number }>(
        `UPDATE mcp_tokens SET revoked_at = now()
         WHERE id = $1 AND user_id = $2 AND revoked_at IS NULL RETURNING id`,
        [id, userId],
      );
      if (!rows[0]) throw new NotesError(`找不到 token：${id}`);
      return { revoked: rows[0].id };
    },
  };
}

export type Users = ReturnType<typeof createUsers>;
