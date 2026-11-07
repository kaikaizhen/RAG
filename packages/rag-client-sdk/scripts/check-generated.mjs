// 驗證 src/generated/schema.ts 與 openapi/rag-api.v1.json 同步（CI / 本機皆可執行）
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import openapiTS, { astToString } from 'openapi-typescript';

const root = new URL('..', import.meta.url);
const snapshot = new URL('openapi/rag-api.v1.json', root);
const committed = readFileSync(new URL('src/generated/schema.ts', root), 'utf8');

const ast = await openapiTS(new URL(snapshot));
const header = committed.slice(0, committed.indexOf('export interface paths'));
const generated = header + astToString(ast);

const normalize = (s) => s.replace(/\r\n/g, '\n').trim();
if (normalize(generated) !== normalize(committed)) {
  console.error(`schema.ts 與 ${fileURLToPath(snapshot)} 不同步：請執行 npm run generate`);
  process.exit(1);
}
console.log('schema.ts 與 OpenAPI snapshot 同步');
