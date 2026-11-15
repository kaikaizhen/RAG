import { readdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import pg from 'pg';

export interface Queryable {
  query<T>(sql: string, params?: unknown[]): Promise<{ rows: T[] }>;
}

/** 正式環境接 pg，測試接 PGlite，兩邊都是真的 PostgreSQL。 */
export interface Database extends Queryable {
  tx<T>(fn: (q: Queryable) => Promise<T>): Promise<T>;
  exec(sql: string): Promise<void>;
  close(): Promise<void>;
}

export function createPgDatabase(connectionString: string): Database {
  const pool = new pg.Pool({ connectionString, max: 5 });
  // 閒置連線斷掉時不要讓整個程式崩潰，下次查詢會自動重連。
  pool.on('error', (err) => console.error('資料庫連線錯誤:', err.message));

  return {
    async query<T>(sql: string, params: unknown[] = []) {
      const result = await pool.query(sql, params);
      return { rows: result.rows as T[] };
    },
    async tx(fn) {
      const client = await pool.connect();
      try {
        await client.query('BEGIN');
        const result = await fn({
          async query<T>(sql: string, params: unknown[] = []) {
            const r = await client.query(sql, params);
            return { rows: r.rows as T[] };
          },
        });
        await client.query('COMMIT');
        return result;
      } catch (err) {
        await client.query('ROLLBACK');
        throw err;
      } finally {
        client.release();
      }
    },
    async exec(sql) {
      await pool.query(sql);
    },
    close: () => pool.end(),
  };
}

const MIGRATIONS_DIR = fileURLToPath(new URL('../migrations/', import.meta.url));

/** 依檔名順序執行還沒跑過的 migration，每個檔案在一個交易裡。 */
export async function migrate(db: Database): Promise<string[]> {
  await db.exec(`CREATE TABLE IF NOT EXISTS schema_migrations (
    name text PRIMARY KEY,
    applied_at timestamptz NOT NULL DEFAULT now()
  )`);

  const { rows } = await db.query<{ name: string }>('SELECT name FROM schema_migrations');
  const done = new Set(rows.map((r) => r.name));
  const files = (await readdir(MIGRATIONS_DIR)).filter((f) => f.endsWith('.sql')).sort();

  const applied: string[] = [];
  for (const file of files) {
    if (done.has(file)) continue;
    const sql = await readFile(path.join(MIGRATIONS_DIR, file), 'utf8');
    await db.exec(`BEGIN;\n${sql}\n;INSERT INTO schema_migrations (name) VALUES ('${file}');\nCOMMIT;`);
    applied.push(file);
  }
  return applied;
}
