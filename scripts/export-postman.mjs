// 由 OpenAPI snapshot 產生可直接匯入 Postman 的檔案（docs/api/）。
// 用法：node scripts/export-postman.mjs
// 輸出只含結構與範例，不含任何帳密或 token；登入後 token 由 Postman 的測試腳本存進 collection 變數。
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';

const SOURCE = 'packages/rag-client-sdk/openapi/rag-api.v1.json';
const OUT = 'RAG/docs';
const PUBLIC_URL = 'https://rag.name404.com';
const PUBLIC_PATHS = new Set(['/api/v1/Auth/Login', '/api/v1/Auth/Refresh', '/api/v1/Auth/Register', '/api/v1/Auth/VerifyEmail']);
const SPACE_HEADER = 'X-Space-Id';

const spec = JSON.parse(readFileSync(SOURCE, 'utf8'));
const schemas = spec.components?.schemas ?? {};

const resolve = (s) => (s?.$ref ? schemas[s.$ref.split('/').pop()] : s);

function example(schema, depth = 0) {
  const s = resolve(schema);
  if (!s || depth > 4) return null;
  if (s.enum) return s.enum[0];
  switch (s.type) {
    case 'object': return Object.fromEntries(Object.entries(s.properties ?? {}).map(([k, v]) => [k, example(v, depth + 1)]));
    case 'array': return [example(s.items, depth + 1)];
    case 'integer': case 'number': return 1;
    case 'boolean': return true;
    case 'string': return s.format === 'date-time' ? '2026-01-01T00:00:00Z' : 'string';
    default: return null;
  }
}

// 常用請求的實際可用範例（覆蓋由 schema 推導的泛用值）
const BODY = {
  'POST /api/v1/Auth/Login': { account: '{{account}}', password: '{{password}}' },
  'POST /api/v1/Auth/Refresh': { refreshToken: '{{refreshToken}}' },
  'POST /api/v1/Chat': { question: '什麼是三層式架構？' },
  'POST /api/v1/Chat/ChatStream': { question: '什麼是三層式架構？' },
};

// ---- OpenAPI（給 Postman / Swagger Editor 匯入）----
const openapi = structuredClone(spec);
openapi.info.description = '登入後取得 accessToken，以 Bearer 帶入。選用標頭 X-Space-Id 指定 Space（不帶則使用預設 Space）；授權一律由伺服器判斷。';
openapi.servers = [{ url: PUBLIC_URL, description: 'NAS（經 Nginx /api/）' }, { url: 'http://localhost:5115', description: '本機' }];
openapi.security = [{ Bearer: [] }];
for (const [path, ops] of Object.entries(openapi.paths)) {
  for (const op of Object.values(ops)) {
    if (PUBLIC_PATHS.has(path)) op.security = [];
    else (op.parameters ??= []).push({ name: SPACE_HEADER, in: 'header', required: false, description: 'Space 編號（hint）', schema: { type: 'integer' } });
  }
}

// ---- Postman Collection v2.1 ----
const folders = new Map();
for (const [path, ops] of Object.entries(spec.paths)) {
  for (const [method, op] of Object.entries(ops)) {
    const segments = path.replace(/^\//, '').split('/').map((s) => (s.startsWith('{') ? `:${s.slice(1, -1)}` : s));
    const query = (op.parameters ?? []).filter((p) => p.in === 'query').map((p) => ({ key: p.name, value: p.name === 'Type' ? '0' : '', description: p.description || undefined, disabled: p.name !== 'Type' }));
    const variable = (op.parameters ?? []).filter((p) => p.in === 'path').map((p) => ({ key: p.name, value: '1' }));
    const header = [{ key: SPACE_HEADER, value: '{{spaceId}}', disabled: true, description: '指定 Space；留空 / 停用則使用預設 Space' }];
    const json = op.requestBody?.content?.['application/json']?.schema;
    const key = `${method.toUpperCase()} ${path}`;
    const request = {
      method: method.toUpperCase(),
      header,
      url: {
        raw: `{{baseUrl}}/${segments.join('/')}${query.length ? '?' + query.filter((q) => !q.disabled).map((q) => `${q.key}=${q.value}`).join('&') : ''}`,
        host: ['{{baseUrl}}'], path: segments, query, variable,
      },
      description: op.summary || undefined,
    };
    if (PUBLIC_PATHS.has(path)) request.auth = { type: 'noauth' };
    if (json) {
      header.push({ key: 'Content-Type', value: 'application/json' });
      request.body = { mode: 'raw', raw: JSON.stringify(BODY[key] ?? example(json), null, 2), options: { raw: { language: 'json' } } };
    }
    const item = { name: `${method.toUpperCase()} ${path.replace('/api/v1', '')}`, request };
    if (path === '/api/v1/Auth/Login' || path === '/api/v1/Auth/Refresh') {
      item.event = [{ listen: 'test', script: { type: 'text/javascript', exec: [
        'if (pm.response.code === 200) {',
        '  const r = pm.response.json();',
        "  pm.collectionVariables.set('accessToken', r.accessToken);",
        "  pm.collectionVariables.set('refreshToken', r.refreshToken);",
        '}',
      ] } }];
    }
    const tag = op.tags?.[0] ?? '其他';
    if (!folders.has(tag)) folders.set(tag, []);
    folders.get(tag).push(item);
  }
}
// 登入放最前面，匯入後第一個就能跑
const items = [...folders.entries()].sort(([a], [b]) => (a.includes('驗證') ? -1 : b.includes('驗證') ? 1 : 0)).map(([name, item]) => ({ name, item }));

const collection = {
  info: {
    name: 'RAG API（.NET 後端）',
    description: '1) 到 Collection 的 Variables 填 account / password；2) 執行「驗證功能模組 → POST /Auth/Login」，token 會自動存入；3) 其餘請求自動帶 Bearer。要切換 Space 時，啟用請求上的 X-Space-Id 標頭並填 spaceId。access token 過期（3 小時）時執行 /Auth/Refresh。',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
  },
  auth: { type: 'bearer', bearer: [{ key: 'token', value: '{{accessToken}}', type: 'string' }] },
  variable: [
    { key: 'baseUrl', value: PUBLIC_URL },
    { key: 'account', value: '' },
    { key: 'password', value: '' },
    { key: 'spaceId', value: '' },
    { key: 'accessToken', value: '' },
    { key: 'refreshToken', value: '' },
  ],
  item: items,
};

mkdirSync(OUT, { recursive: true });
writeFileSync(`${OUT}/rag-api.openapi.json`, JSON.stringify(openapi, null, 2) + '\n');
writeFileSync(`${OUT}/rag-api.postman_collection.json`, JSON.stringify(collection, null, 2) + '\n');
console.log(`${Object.values(spec.paths).reduce((n, o) => n + Object.keys(o).length, 0)} 個端點 → ${OUT}/`);
