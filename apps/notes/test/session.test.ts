import { describe, expect, it } from 'vitest';
import { createSession, SESSION_DAYS, verifySession } from '../web/lib/session.js';

const SECRET = 'a'.repeat(32);
const DAY = 24 * 60 * 60 * 1000;

describe('登入 session', () => {
  it('自己簽的可以通過', async () => {
    expect(await verifySession(await createSession(SECRET, 7), SECRET)).toBe(7);
  });

  it('到期後失效', async () => {
    const now = Date.now();
    const session = await createSession(SECRET, 7, now);
    expect(await verifySession(session, SECRET, now + (SESSION_DAYS - 1) * DAY)).toBe(7);
    expect(await verifySession(session, SECRET, now + (SESSION_DAYS + 1) * DAY)).toBeNull();
  });

  it('竄改到期時間、使用者或換密鑰都不能通過', async () => {
    const session = await createSession(SECRET, 7);
    const [expires, , signature] = session.split('.');
    expect(await verifySession(`9999999999999.7.${signature}`, SECRET)).toBeNull();
    expect(await verifySession(`${expires}.8.${signature}`, SECRET)).toBeNull();
    expect(await verifySession(session, 'b'.repeat(32))).toBeNull();
  });

  it('空值、格式錯誤、沒設定密鑰都不能通過', async () => {
    const session = await createSession(SECRET, 7);
    for (const value of [undefined, '', 'abc', '.', 'abc.def', `${session}.extra`]) {
      expect(await verifySession(value, SECRET), String(value)).toBeNull();
    }
    expect(await verifySession(session, undefined)).toBeNull();
  });
});
