import { CircleHelp } from 'lucide-react';
import Link from 'next/link';
import { RuleGroups } from '@/components/RuleGroups';
import { Empty, one, PageHeader, type SearchParams } from '@/components/ui';
import { getStats, listConcepts, listGaps, listRules } from '@/lib/api';

export default async function QuestionsPage({ searchParams }: { searchParams: SearchParams }) {
  const notice = one((await searchParams).gap);
  const [stats, { concepts }, { rules }, { gaps }] = await Promise.all([
    getStats(),
    listConcepts({ has_open_questions: true, limit: 50 }),
    listRules({ confidence: 'code_inferred' }),
    listGaps(),
  ]);
  const total = stats.general_definition + stats.code_inferred + stats.colleague_confirmed;
  const confirmedPercent = total === 0 ? 0 : Math.round((stats.colleague_confirmed / total) * 100);

  return (
    <>
      <PageHeader title="待確認" icon={<CircleHelp size={18} strokeWidth={1.8} aria-hidden="true" />}>
        問過同事後，請 Claude 更新可信度並記下向誰確認。
      </PageHeader>

      <div className="progress-card">
        <div className="progress-text">
          <strong>{confirmedPercent}%</strong>
          <span>
            已向同事確認 {stats.colleague_confirmed} / {total} 筆
          </span>
        </div>
        <div className="progress-track" role="img" aria-label={`已確認 ${confirmedPercent}%`}>
          <span style={{ width: `${confirmedPercent}%` }} />
        </div>
      </div>

      {notice === 'added' && <p className="notice">記下來了。你的 Claude 下次工作時會看到這題。</p>}
      {notice === 'rejected' && <p className="notice is-error">這個問題沒有記下來，可能含有密碼或 email 這類不該進筆記的內容。</p>}

      <h2 className="section-title">
        問答答不出來的<span className="count">{gaps.length}</span>
      </h2>
      {gaps.length === 0 ? (
        <Empty>目前沒有。問答答不出來時，按「交給筆記」就會出現在這裡。</Empty>
      ) : (
        <div className="question-list">
          {gaps.map((gap) => (
            <article key={gap.id} className="question-row gap-row">
              <h3>
                <CircleHelp size={14} strokeWidth={1.9} aria-hidden="true" />
                {gap.question}
              </h3>
              <form action="/gaps/submit" method="post">
                <input type="hidden" name="resolve" value={gap.id} />
                <button type="submit" className="ghost-button">
                  已解決
                </button>
              </form>
            </article>
          ))}
        </div>
      )}

      <h2 className="section-title">
        待釐清的觀念<span className="count">{concepts.length}</span>
      </h2>
      {concepts.length === 0 ? (
        <Empty>目前沒有待釐清的觀念。</Empty>
      ) : (
        <div className="question-list">
          {concepts.map((concept) => (
            <article key={concept.id} className="question-row">
              <h3>
                <CircleHelp size={14} strokeWidth={1.9} aria-hidden="true" />
                <Link href={`/concepts?id=${concept.id}`}>{concept.term}</Link>
              </h3>
              <p>{concept.open_questions}</p>
            </article>
          ))}
        </div>
      )}

      <h2 className="section-title">
        尚未確認的規則<span className="count">{rules.length}</span>
      </h2>
      {rules.length === 0 ? <Empty>所有規則都確認過了。</Empty> : <RuleGroups rules={rules} />}
    </>
  );
}
