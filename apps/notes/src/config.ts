export interface Config {
  databaseUrl: string;
  mcpToken: string;
  apiToken: string;
  ownerRagUserId?: number;
  syncKey?: string;
  ragApiUrl?: string;
  publicSpace?: { account: string; password: string; spaceId: number };
  port: number;
  host: string;
}

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  const databaseUrl = env.DATABASE_URL;
  if (!databaseUrl) throw new Error('缺少環境變數 DATABASE_URL');

  const mcpToken = env.MCP_TOKEN;
  if (!mcpToken || mcpToken.length < 32) {
    throw new Error('環境變數 MCP_TOKEN 必須至少 32 字元');
  }

  const apiToken = env.API_TOKEN;
  if (!apiToken || apiToken.length < 32) {
    throw new Error('環境變數 API_TOKEN 必須至少 32 字元');
  }

  const ownerRagUserId = env.NOTES_OWNER_RAG_USER_ID ? Number(env.NOTES_OWNER_RAG_USER_ID) : undefined;
  if (ownerRagUserId !== undefined && (!Number.isInteger(ownerRagUserId) || ownerRagUserId < 1)) {
    throw new Error('環境變數 NOTES_OWNER_RAG_USER_ID 必須是正整數');
  }

  const syncKey = env.SYNC_KEY || undefined;
  if (syncKey && syncKey.length < 32) throw new Error('環境變數 SYNC_KEY 必須至少 32 字元');

  const publicSpaceId = Number(env.RAG_PUBLIC_SPACE_ID);
  const publicSpace =
    env.RAG_PUBLIC_ACCOUNT && env.RAG_PUBLIC_PASSWORD && Number.isInteger(publicSpaceId) && publicSpaceId > 0
      ? { account: env.RAG_PUBLIC_ACCOUNT, password: env.RAG_PUBLIC_PASSWORD, spaceId: publicSpaceId }
      : undefined;

  const port = Number(env.PORT ?? 3100);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error('環境變數 PORT 不是有效的埠號');
  }

  return { databaseUrl, mcpToken, apiToken, ownerRagUserId, syncKey, ragApiUrl: env.RAG_API_URL || undefined, publicSpace, port, host: env.HOST ?? '127.0.0.1' };
}
