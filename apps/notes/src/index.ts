import { serve } from '@hono/node-server';
import { createApp } from './app.js';
import { loadConfig } from './config.js';
import { createPgDatabase, migrate } from './db.js';
import { createRagWorker, httpClientFactory } from './ragWorker.js';
import { createUsers } from './users.js';

const config = loadConfig();
const db = createPgDatabase(config.databaseUrl);

const applied = await migrate(db);
if (applied.length > 0) console.log(`已套用 migration：${applied.join('、')}`);

const users = createUsers(db, config.ownerRagUserId, config.syncKey);
const worker =
  config.syncKey && config.ragApiUrl
    ? createRagWorker({ db, users, clientFor: httpClientFactory(config.ragApiUrl), publicSpace: config.publicSpace })
    : undefined;
const app = createApp({ db, users, worker, mcpToken: config.mcpToken, apiToken: config.apiToken });
if (worker) {
  await worker.markAllDirty();
  setInterval(() => void worker.tick().catch((err) => console.error('背景同步失敗:', err)), 60_000).unref();
} else {
  console.log('沒有設定 SYNC_KEY 或 RAG_API_URL，背景同步不會啟動');
}
const server = serve({ fetch: app.fetch, port: config.port, hostname: config.host }, (info) => {
  console.log(`learning-notes 已啟動：http://${info.address}:${info.port}`);
});

function shutdown() {
  server.close(() => {
    void db.close().finally(() => process.exit(0));
  });
}
process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
