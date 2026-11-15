import { exportNotes } from '@/lib/api';

/** 下載自己的全部筆記（JSON）。只匯出登入者本人的資料。 */
export async function GET() {
  const date = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Taipei' }).format(new Date());
  return new Response(JSON.stringify(await exportNotes(), null, 2), {
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'content-disposition': `attachment; filename="learning-notes-${date}.json"`,
      'cache-control': 'no-store',
    },
  });
}
