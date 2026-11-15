import type { Database } from './db.js';
import {
  applyPlan,
  buildPlan,
  createHttpRagClient,
  loadNoteDocs,
  noteKey,
  resolveCategories,
  type NoteDoc,
  type RagClient,
} from './ragSync.js';
import type { Users } from './users.js';

type SyncClient = RagClient & { refreshToken?: string };

export type ClientFactory = (auth: { refreshToken: string } | { account: string; password: string; spaceId: number }) => Promise<SyncClient>;

export function httpClientFactory(baseUrl: string): ClientFactory {
  return (auth) => createHttpRagClient({ baseUrl, ...auth });
}

/** 讓 RAG 上標記過的問答和 docs 一致：docs 沒有的就刪掉，專案改成不分享時內容才會真的離開 RAG。 */
async function pushDocs(client: SyncClient, docs: NoteDoc[]) {
  const itemIds = await resolveCategories(client, true);
  if (!itemIds) throw new Error('無法建立 RAG 分類');
  const plan = buildPlan(docs, await client.listQuestions(), itemIds, { existingKeys: new Set(docs.map(noteKey)) });
  await applyPlan(client, plan);
}

async function projectsWith(db: Database, sharing: string, ownerId?: number) {
  const { rows } = await db.query<{ owner_id: number; name: string }>(
    `SELECT owner_id, name FROM projects WHERE sharing = $1 ${ownerId === undefined ? '' : 'AND owner_id = $2'}`,
    ownerId === undefined ? [sharing] : [sharing, ownerId],
  );
  return rows;
}

export function createRagWorker(deps: {
  db: Database;
  users: Users;
  clientFor: ClientFactory;
  publicSpace?: { account: string; password: string; spaceId: number };
}) {
  const dirty = new Set<number>();
  let publicDirty = true;
  let running = false;

  async function syncUser(userId: number) {
    const refreshToken = await deps.users.refreshTokenFor(userId);
    if (!refreshToken) return;
    try {
      const projects = (await projectsWith(deps.db, 'private', userId)).map((p) => p.name);
      const docs = projects.length > 0 ? await loadNoteDocs(deps.db, userId, { projects }) : [];
      const client = await deps.clientFor({ refreshToken });
      if (client.refreshToken) await deps.users.saveRefreshToken(userId, client.refreshToken);
      await pushDocs(client, docs);
      await deps.users.recordSync(userId, null);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      // refresh token 過期就停止重試，等使用者下次登入換一組新的
      if (/Auth\/Refresh.*401/.test(message)) {
        await deps.users.clearRefreshToken(userId);
        await deps.users.recordSync(userId, '問答系統的登入已過期，請重新登入筆記網站');
      } else {
        console.error(`同步使用者 ${userId} 失敗:`, message);
        await deps.users.recordSync(userId, '同步失敗，稍後會自動再試');
        dirty.add(userId);
      }
    }
  }

  async function syncPublic() {
    if (!deps.publicSpace) return;
    const byOwner = new Map<number, string[]>();
    for (const project of await projectsWith(deps.db, 'public')) {
      byOwner.set(project.owner_id, [...(byOwner.get(project.owner_id) ?? []), project.name]);
    }
    const docs: NoteDoc[] = [];
    for (const [ownerId, projects] of byOwner) docs.push(...(await loadNoteDocs(deps.db, ownerId, { projects, forPublic: true })));
    await pushDocs(await deps.clientFor(deps.publicSpace), docs);
  }

  return {
    syncUser,
    syncPublic,

    markDirty(userId: number) {
      dirty.add(userId);
      publicDirty = true;
    },

    /** 服務重啟時不知道哪些人改過，全部比對一次；內容沒變就不會寫入。 */
    async markAllDirty() {
      for (const id of await deps.users.linkedUserIds()) dirty.add(id);
      publicDirty = true;
    },

    async tick() {
      if (running) return;
      running = true;
      try {
        for (const userId of [...dirty]) {
          dirty.delete(userId);
          await syncUser(userId);
        }
        if (publicDirty) {
          publicDirty = false;
          await syncPublic().catch((err) => {
            publicDirty = true;
            console.error('同步公開知識失敗:', err instanceof Error ? err.message : err);
          });
        }
      } finally {
        running = false;
      }
    },
  };
}

export type RagWorker = ReturnType<typeof createRagWorker>;
