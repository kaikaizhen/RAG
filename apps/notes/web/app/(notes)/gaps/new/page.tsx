import { CircleHelp } from 'lucide-react';
import Link from 'next/link';
import { one, PageHeader, type SearchParams } from '@/components/ui';

/** 問答答不出來時從問答網站連過來。一定要使用者按下確認才寫入，別的網站沒辦法靠連結塞問題進來。 */
export default async function NewGapPage({ searchParams }: { searchParams: SearchParams }) {
  const question = (one((await searchParams).q) ?? '').trim().slice(0, 500);

  return (
    <>
      <PageHeader title="把問題交給筆記" icon={<CircleHelp size={18} strokeWidth={1.8} aria-hidden="true" />}>
        記下來後，你的 Claude 下次工作時會看到，並提議把答案補成筆記。
      </PageHeader>
      <form action="/gaps/submit" method="post" className="gap-form">
        <label htmlFor="question">問題</label>
        <textarea id="question" name="question" defaultValue={question} maxLength={500} rows={3} required />
        <div className="gap-actions">
          <button type="submit" className="primary-button">
            記下這個問題
          </button>
          <Link href="/questions" className="ghost-button">
            先不要
          </Link>
        </div>
      </form>
    </>
  );
}
