import { NotesError } from './errors.js';

/**
 * 寫入前擋下密碼、連線字串和 email。筆記是給人看的，這些東西不該進來。
 * 這是安全網，不是保證：姓名、電話、身分證這類格式和料號太像，沒有自動偵測。
 */
const PATTERNS: Array<[kind: string, pattern: RegExp]> = [
  ['含帳密的網址', /[a-z][a-z0-9+.-]*:\/\/[^\s/:@]+:[^\s/@]+@/i],
  ['資料庫連線字串', /\b(?:data source|initial catalog|server|host)\s*=[^;\n]+;[^\n]*\b(?:user id|uid|username|password|pwd)\s*=/i],
  // 值要有數字或符號才算，才不會擋到「password: required」這種敘述；@、: 開頭是 SQL 參數。
  ['密碼或金鑰', /(?:\b(?:password|passwd|pwd|secret|api[_-]?key|access[_-]?token|auth[_-]?token)\b|密碼|金鑰)\s*(?:=|:|：)\s*["']?(?![@:$<{])(?=[^\s"',;]*[\d!#$%^&*])[^\s"',;]{6,}/i],
  ['私鑰', /-----BEGIN [A-Z ]*PRIVATE KEY-----/],
  ['GitHub token', /\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})\b/],
  ['API 金鑰', /\b(?:sk-[A-Za-z0-9_-]{20,}|AKIA[0-9A-Z]{16}|xox[abprs]-[A-Za-z0-9-]{10,})\b/],
  ['JWT', /\beyJ[\w-]{10,}\.[\w-]{10,}\.[\w-]{10,}\b/],
  ['Bearer token', /\bbearer\s+[A-Za-z0-9._~+/-]{20,}/i],
  ['email', /[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}/],
];

function* walk(value: unknown, path: string): Generator<[string, string]> {
  if (typeof value === 'string') {
    yield [path, value];
  } else if (Array.isArray(value)) {
    for (const [i, item] of value.entries()) yield* walk(item, `${path}[${i}]`);
  } else if (value && typeof value === 'object') {
    for (const [key, item] of Object.entries(value)) yield* walk(item, path ? `${path}.${key}` : key);
  }
}

export function assertNoSecrets(input: unknown): void {
  const problems: string[] = [];
  for (const [path, text] of walk(input, '')) {
    for (const [kind, pattern] of PATTERNS) {
      if (pattern.test(text)) problems.push(`${path}（疑似${kind}）`);
    }
  }
  if (problems.length > 0) {
    // 只說哪個欄位，不把疑似機密的內容寫回錯誤訊息或 log。
    throw new NotesError(
      `沒有寫入。以下欄位含有不該放進筆記的內容，請移除或改成描述後再送一次：${problems.join('、')}`,
    );
  }
}
