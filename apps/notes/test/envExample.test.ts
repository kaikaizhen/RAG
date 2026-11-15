import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

// .env.example 會進 git，曾經不小心把真的 token 填進去。這裡擋住機密欄位有值的情況。
const SECRET_KEYS = [
  'MCP_TOKEN',
  'API_TOKEN',
  'SESSION_SECRET',
  'POSTGRES_PASSWORD',
  'DATABASE_URL',
  'RAG_ACCOUNT',
  'RAG_PASSWORD',
  'SYNC_KEY',
  'RAG_PUBLIC_ACCOUNT',
  'RAG_PUBLIC_PASSWORD',
];

describe('.env.example', () => {
  it('機密欄位都是空的', () => {
    const filled = readFileSync(new URL('../.env.example', import.meta.url), 'utf8')
      .split(/\r?\n/)
      .map((line) => /^([A-Z_]+)=(.*)$/.exec(line.trim()))
      .filter((match): match is RegExpExecArray => match !== null && SECRET_KEYS.includes(match[1]!) && match[2]!.trim() !== '')
      .map((match) => match[1]);
    expect(filled).toEqual([]);
  });
});
