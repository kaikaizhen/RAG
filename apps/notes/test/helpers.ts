import { PGlite } from '@electric-sql/pglite';
import { pg_trgm } from '@electric-sql/pglite/contrib/pg_trgm';
import { migrate, type Database } from '../src/db.js';

/** 真的 PostgreSQL（WASM），每次測試都是全新的資料庫，不需要 Docker。 */
/** 建立一位測試使用者，回傳 id。 */
export async function createTestUser(db: Database, name = '測試使用者'): Promise<number> {
  const { rows } = await db.query<{ id: number }>('INSERT INTO users (name) VALUES ($1) RETURNING id', [name]);
  return rows[0]!.id;
}

export async function createTestDatabase(): Promise<Database> {
  const pg = new PGlite({ extensions: { pg_trgm } });
  const db: Database = {
    query: async <T>(sql: string, params: unknown[] = []) => ({
      rows: (await pg.query<T>(sql, params)).rows,
    }),
    tx: async <T>(fn: Parameters<Database['tx']>[0]) =>
      (await pg.transaction((tx) =>
        fn({
          query: async <R>(sql: string, params: unknown[] = []) => ({
            rows: (await tx.query<R>(sql, params)).rows,
          }),
        }),
      )) as T,
    exec: async (sql) => {
      await pg.exec(sql);
    },
    close: () => pg.close(),
  };
  await migrate(db);
  return db;
}
