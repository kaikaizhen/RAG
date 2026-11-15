import { ChevronLeft, ChevronRight } from 'lucide-react';
import Link from 'next/link';
import type { ReactNode } from 'react';
import type { Confidence, Project, TestStatus } from '@/lib/api';

const CONFIDENCE_LABEL: Record<Confidence, string> = {
  general_definition: '一般定義',
  code_inferred: '程式碼推測',
  colleague_confirmed: '已向同事確認',
};

export const TEST_STATUS_LABEL: Record<TestStatus, string> = {
  not_tested: '還沒測',
  manual: '手動測過',
  automated: '自動化測試',
  production_verified: '正式環境確認',
};

export function ConfidenceBadge({ confidence, confirmedBy }: { confidence: Confidence; confirmedBy?: string | null }) {
  return (
    <span className={`badge badge-${confidence}`}>
      {CONFIDENCE_LABEL[confidence]}
      {confirmedBy ? `・${confirmedBy}` : ''}
    </span>
  );
}

export function TestBadge({ status }: { status: TestStatus }) {
  return <span className={`badge badge-test-${status}`}>{TEST_STATUS_LABEL[status]}</span>;
}

/** 專案名稱換算成固定的顏色編號，同一個專案在每一頁都同色。 */
export function projectTone(name: string): number {
  let hash = 2166136261;
  for (const char of name) {
    hash ^= char.codePointAt(0) ?? 0;
    hash = Math.imul(hash, 16777619);
  }
  return ((hash >>> 0) % 8) + 1;
}

/** 專案裡的紀錄、筆記、觀念卡和規則總數。沒有改動紀錄的專案也算有內容。 */
export function projectSize(project: Project): number {
  return project.entry_count + project.article_count + project.concept_count + project.rule_count;
}

export function projectBreakdown(project: Project): string {
  return `${project.entry_count} 筆紀錄 · ${project.article_count} 篇筆記 · ${project.concept_count} 張觀念卡 · ${project.rule_count} 條規則`;
}

/** 點專案時先去有東西的那一頁：紀錄、筆記、觀念卡、規則依序。 */
export function projectHref(project: Project): string {
  const name = encodeURIComponent(project.name);
  if (project.entry_count > 0) return `/entries?project=${name}`;
  if (project.article_count > 0) return `/articles?project=${name}`;
  if (project.concept_count > 0) return `/concepts?project=${name}`;
  return `/rules?project=${name}`;
}

export function ProjectTag({ name, detail }: { name: string; detail?: string | null }) {
  return (
    <span className={`project-tag tone-${projectTone(name)}`}>
      <i aria-hidden="true" />
      {name}
      {detail ? <span className="project-detail">{detail}</span> : null}
    </span>
  );
}

export function Field({ label, icon, children }: { label: string; icon?: ReactNode; children: ReactNode }) {
  if (!children) return null;
  return (
    <div className="field">
      <dt>
        {icon}
        {label}
      </dt>
      <dd>{children}</dd>
    </div>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="empty">{children}</p>;
}

/** 完全沒有資料時的提示，順便告訴還沒接上 Claude 的人去哪裡設定。 */
export function NoNotesYet({ what }: { what: string }) {
  return (
    <Empty>
      還沒有{what}。跟你的 Claude 說要記什麼就好；還沒接上的話，先到 <Link href="/tokens">Token 頁</Link>設定。
    </Empty>
  );
}

/** 這一頁剛好滿了才可能有下一頁，所以用筆數判斷。 */
export function Pager({
  path,
  params,
  offset,
  pageSize,
  count,
}: {
  path: string;
  params: Record<string, string | undefined>;
  offset: number;
  pageSize: number;
  count: number;
}) {
  const hrefFor = (next: number) => {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) if (value) query.set(key, value);
    if (next > 0) query.set('offset', String(next));
    const text = query.toString();
    return text ? `${path}?${text}` : path;
  };
  if (offset === 0 && count < pageSize) return null;
  return (
    <nav className="pager" aria-label="換頁">
      {offset > 0 ? (
        <Link className="ghost-button" href={hrefFor(Math.max(offset - pageSize, 0))}>
          <ChevronLeft size={16} aria-hidden="true" />
          上一頁
        </Link>
      ) : (
        <span />
      )}
      {count === pageSize ? (
        <Link className="ghost-button" href={hrefFor(offset + pageSize)}>
          下一頁
          <ChevronRight size={16} aria-hidden="true" />
        </Link>
      ) : (
        <span />
      )}
    </nav>
  );
}

/** 從外部（例如 RAG 的來源標籤）用 ?id= 連進來時，說明現在只顯示一篇，並提供回到全部的路。 */
export function SingleView({ backHref, backLabel }: { backHref: string; backLabel: string }) {
  return (
    <p className="single-view">
      <span>只顯示連結指向的這一篇</span>
      <Link href={backHref}>{backLabel}</Link>
    </p>
  );
}

export type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export function one(value: string | string[] | undefined): string | undefined {
  const text = Array.isArray(value) ? value[0] : value;
  return text?.trim() || undefined;
}

export function offsetFrom(value: string | string[] | undefined): number {
  const n = Number(one(value));
  return Number.isInteger(n) && n > 0 ? n : 0;
}

export function PageHeader({ title, icon, children }: { title: string; icon: ReactNode; children?: ReactNode }) {
  return (
    <header className="page-head">
      <span className="page-icon">{icon}</span>
      <div>
        <h1>{title}</h1>
        {children && <p>{children}</p>}
      </div>
    </header>
  );
}

/** 用網址切換的篩選標籤，沒有 JS 也能用。 */
export function ChipLinks({ label, items }: { label: string; items: { href: string; text: ReactNode; active: boolean }[] }) {
  return (
    <nav className="chips" aria-label={label}>
      {items.map((item) => (
        <Link key={item.href} href={item.href} className="chip" aria-current={item.active ? 'true' : undefined}>
          {item.text}
        </Link>
      ))}
    </nav>
  );
}
