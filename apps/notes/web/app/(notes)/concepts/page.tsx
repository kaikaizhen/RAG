import { BookOpen, Search } from 'lucide-react';
import Link from 'next/link';
import { ConceptCard } from '@/components/ConceptCard';
import { ChipLinks, Empty, NoNotesYet, one, offsetFrom, PageHeader, Pager, projectTone, SingleView, type SearchParams } from '@/components/ui';
import { listConcepts, listProjects, PAGE_SIZE } from '@/lib/api';

const CONFIDENCE_FILTERS = [
  { value: undefined, text: '全部' },
  { value: 'colleague_confirmed', text: '已向同事確認' },
  { value: 'general_definition', text: '一般定義' },
  { value: 'code_inferred', text: '程式碼推測' },
];

export default async function ConceptsPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const query = one(params.query);
  const confidence = CONFIDENCE_FILTERS.find((filter) => filter.value === one(params.confidence))?.value;
  // ?id= 是外部（例如 RAG 的來源標籤）連進來看單張用的
  const id = /^\d+$/.test(one(params.id) ?? '') ? one(params.id) : undefined;
  const offset = offsetFrom(params.offset);
  const [{ count, concepts }, { projects }] = await Promise.all([
    listConcepts({ id, query, confidence, project: one(params.project), offset }),
    listProjects(),
  ]);
  const withConcepts = projects.filter((p) => p.concept_count > 0);
  const project = withConcepts.find((p) => p.name === one(params.project))?.name;

  // 換一種篩選時保留其他條件，回到第一頁
  const hrefFor = (change: { query?: null; confidence?: string | null; project?: string | null }) => {
    const search = new URLSearchParams();
    if (query && change.query !== null) search.set('query', query);
    const nextConfidence = change.confidence === undefined ? confidence : change.confidence;
    const nextProject = change.project === undefined ? project : change.project;
    if (nextConfidence) search.set('confidence', nextConfidence);
    if (nextProject) search.set('project', nextProject);
    const text = search.toString();
    return text ? `/concepts?${text}` : '/concepts';
  };

  return (
    <>
      <PageHeader title="詞彙表" icon={<BookOpen size={18} strokeWidth={1.8} aria-hidden="true" />}>
        每個觀念的白話解釋、例子和它在系統裡的位置。
      </PageHeader>
      <form className="filters" action="/concepts" method="get">
        <span className="search-box">
          <Search size={15} aria-hidden="true" />
          <input type="search" name="query" defaultValue={query} placeholder="搜尋觀念，例如 料號" aria-label="搜尋觀念" />
        </span>
        {confidence && <input type="hidden" name="confidence" value={confidence} />}
        {project && <input type="hidden" name="project" value={project} />}
        <button type="submit" className="ghost-button">
          搜尋
        </button>
        {query && (
          <Link href={hrefFor({ query: null })} className="clear-filters">
            清除搜尋
          </Link>
        )}
      </form>
      {!id && withConcepts.length > 0 && (
        <ChipLinks
          label="依專案篩選"
          items={[
            { href: hrefFor({ project: null }), text: '全部專案', active: !project },
            ...withConcepts.map((p) => ({
              href: hrefFor({ project: p.name }),
              text: (
                <span className={`chip-project tone-${projectTone(p.name)}`}>
                  <i aria-hidden="true" />
                  {p.name}
                  <small>{p.concept_count}</small>
                </span>
              ),
              active: p.name === project,
            })),
          ]}
        />
      )}
      {!id && (
        <ChipLinks
          label="依來源篩選"
          items={CONFIDENCE_FILTERS.map((filter) => ({
            href: hrefFor({ confidence: filter.value ?? null }),
            text: filter.text,
            active: filter.value === confidence,
          }))}
        />
      )}
      {id && <SingleView backHref="/concepts" backLabel="看全部觀念" />}
      {concepts.length === 0 ? (
        id || query || confidence || project ? (
          <Empty>{id ? '這張觀念卡已經不存在，可能被刪除了。' : '沒有符合的觀念卡。'}</Empty>
        ) : (
          <NoNotesYet what="觀念卡" />
        )
      ) : (
        <div className="concept-list">
          {concepts.map((concept) => (
            <ConceptCard key={concept.id} concept={concept} />
          ))}
        </div>
      )}
      <Pager path="/concepts" params={{ id, query, confidence, project }} offset={offset} pageSize={PAGE_SIZE} count={count} />
    </>
  );
}
