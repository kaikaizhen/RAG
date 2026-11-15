import { Globe } from 'lucide-react';
import Link from 'next/link';
import { Empty, one, PageHeader, type SearchParams } from '@/components/ui';
import { listPublic } from '@/lib/api';

export default async function ExplorePage({ searchParams }: { searchParams: SearchParams }) {
  const project = one((await searchParams).project);
  const { projects, entries, rules } = await listPublic(project);

  return (
    <>
      <PageHeader title="公開知識" icon={<Globe size={18} strokeWidth={1.8} aria-hidden="true" />}>
        其他人公開的專案。這些內容在問答系統裡大家都問得到。
      </PageHeader>

      {projects.length === 0 ? (
        <Empty>
          還沒有人公開專案。想分享的話，到 <Link href="/sharing">分享範圍</Link>把專案設成公開。
        </Empty>
      ) : (
        <div className="explore-grid">
          {projects.map((p) => (
            <Link key={p.name} href={`/explore?project=${encodeURIComponent(p.name)}`} className="explore-card" aria-current={p.name === project ? 'true' : undefined}>
              <strong>{p.name}</strong>
              <span>{p.owner} 分享</span>
              <small>
                {p.entry_count} 筆紀錄 · {p.rule_count} 條規則
              </small>
            </Link>
          ))}
        </div>
      )}

      {project && (
        <section className="explore-detail">
          {rules.length > 0 && <h2 className="section-title">規則</h2>}
          {rules.map((rule) => (
            <article key={`${rule.module}/${rule.title}`} className="explore-item">
              <h3>{rule.title}</h3>
              <small>{rule.module}</small>
              <p>{rule.body}</p>
            </article>
          ))}
          {entries.length > 0 && <h2 className="section-title">改動紀錄</h2>}
          {entries.map((entry) => (
            <article key={entry.id} className="explore-item">
              <h3>{entry.title}</h3>
              <small>{entry.happened_on}</small>
              <p>{entry.how_to_explain ?? entry.what_changed}</p>
            </article>
          ))}
          {rules.length + entries.length === 0 && <Empty>這個專案沒有公開的內容，或已經改回私人。</Empty>}
        </section>
      )}
    </>
  );
}
