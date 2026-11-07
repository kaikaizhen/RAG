import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { RagClient } from '../src/index.js';

const srcDir = fileURLToPath(new URL('../src', import.meta.url));

function handwrittenSources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) return name === 'generated' ? [] : handwrittenSources(full);
    return full.endsWith('.ts') ? [full] : [];
  });
}

describe('SDK security boundaries', () => {
  // 只檢查程式碼：移除區塊與行註解（註解中說明「不可使用 X」屬於文件，不是用法）
  const source = handwrittenSources(srcDir)
    .map((f) => readFileSync(f, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, ''))
    .join('\n');

  it.each([
    ['Qdrant / vector store access', /qdrant|6333/i],
    ['direct RAG server endpoints', /8007|questionsToVector|delete_type|["'`]\/ask["'`]/i],
    ['internal service credential', /X-Internal-Api-Key|INTERNAL_API_KEY/i],
    ['capability / tool invocation API', /\/Capability|capabilit|invoke/i],
    ['agent execution API', /\/agent/i],
    ['browser persistent storage', /localStorage|sessionStorage|document\.cookie/],
  ])('contains no %s', (_, pattern) => {
    expect(source).not.toMatch(pattern);
  });

  it('public client surface exposes no capability, tool or agent members', () => {
    const client = new RagClient({ baseUrl: 'http://api.test', fetch: (async () => new Response()) as typeof fetch });
    const members = Object.keys(client).concat(Object.getOwnPropertyNames(Object.getPrototypeOf(client)));

    expect(members.join(',')).not.toMatch(/capabilit|tool|agent|scope/i);
  });

  it('only calls versioned API routes', () => {
    const paths = [...source.matchAll(/['`](\/(?:Auth|Space|Skill|Chat)[^'`]*)['`]/g)].map((m) => m[1]);
    expect(paths.length).toBeGreaterThan(0);
    expect(source).toContain('/api/${API_VERSION}${path}');
  });
});
