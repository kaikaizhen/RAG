import { BookOpen, ChevronDown, Database, FlaskConical, GitBranch, GitPullRequest, Lightbulb, MessageCircle, PenLine } from 'lucide-react';
import type { ReactNode } from 'react';
import type { Entry } from '@/lib/api';
import { Field, ProjectTag, TestBadge } from './ui';

/** 只把 http(s) 當連結，避免內容裡的 javascript: 網址被點到。 */
function safeUrl(url: string | null): string | null {
  return url && /^https?:\/\//i.test(url) ? url : null;
}

const icon = (node: typeof PenLine): ReactNode => {
  const Icon = node;
  return <Icon size={13} strokeWidth={1.9} aria-hidden="true" />;
};

export function EntryItem({ entry, open = false }: { entry: Entry; open?: boolean }) {
  const url = safeUrl(entry.pr_url);
  return (
    <details className="entry" open={open}>
      <summary>
        <time dateTime={entry.happened_on}>{entry.happened_on}</time>
        <span className="entry-main">
          <span className="entry-title">{entry.title}</span>
          <ProjectTag name={entry.project} detail={entry.module} />
        </span>
        <TestBadge status={entry.test_status} />
        <ChevronDown className="entry-chevron" size={16} aria-hidden="true" />
      </summary>
      <dl className="entry-body">
        <Field label="改了什麼" icon={icon(PenLine)}>{entry.what_changed}</Field>
        <Field label="為什麼" icon={icon(Lightbulb)}>{entry.why}</Field>
        <Field label="邏輯判斷" icon={icon(GitBranch)}>{entry.logic_notes}</Field>
        <Field label="資料面" icon={icon(Database)}>{entry.data_notes}</Field>
        <Field label="測試" icon={icon(FlaskConical)}>{entry.test_notes}</Field>
        {entry.how_to_explain && (
          <div className="field field-highlight">
            <dt>
              {icon(MessageCircle)}
              別人問時怎麼答
            </dt>
            <dd>{entry.how_to_explain}</dd>
          </div>
        )}
        <Field label="相關觀念" icon={icon(BookOpen)}>{entry.concepts.length > 0 ? entry.concepts.join('、') : null}</Field>
        <Field label="PR" icon={icon(GitPullRequest)}>{url ? <a href={url} rel="noreferrer">{url}</a> : null}</Field>
      </dl>
    </details>
  );
}

/** 依月份分組。API 已經照日期新到舊排序，只要依序分段。 */
export function EntryGroups({ entries, open = false }: { entries: Entry[]; open?: boolean }) {
  const groups = new Map<string, Entry[]>();
  for (const entry of entries) {
    const key = entry.happened_on.slice(0, 7);
    groups.set(key, [...(groups.get(key) ?? []), entry]);
  }
  return (
    <>
      {[...groups].map(([month, items]) => (
        <section key={month} className="entry-group">
          <h2>
            {month.slice(0, 4)} 年 {Number(month.slice(5))} 月<span className="count">{items.length}</span>
          </h2>
          <div className="entry-list">
            {items.map((entry) => (
              <EntryItem key={entry.id} entry={entry} open={open} />
            ))}
          </div>
        </section>
      ))}
    </>
  );
}
