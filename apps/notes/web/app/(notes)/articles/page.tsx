import { NotebookText, Search } from 'lucide-react';
import Link from 'next/link';
import { ArticleFull, ArticleRow } from '@/components/ArticleView';
import { ChipLinks, Empty, NoNotesYet, one, offsetFrom, PageHeader, Pager, projectTone, SingleView, type SearchParams } from '@/components/ui';
import { listArticles, listProjects, PAGE_SIZE } from '@/lib/api';

export default async function ArticlesPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const query = one(params.query);
  // ?id= 是從總覽、列表或問答的來源標籤連進來看單篇用的
  const id = /^\d+$/.test(one(params.id) ?? '') ? one(params.id) : undefined;
  const offset = offsetFrom(params.offset);
  const [{ count, articles }, { projects }] = await Promise.all([
    listArticles({ id, query, project: one(params.project), offset }),
    listProjects(),
  ]);
  const withArticles = projects.filter((p) => p.article_count > 0);
  const project = withArticles.find((p) => p.name === one(params.project))?.name;

  const hrefFor = (change: { query?: null; project?: string | null }) => {
    const search = new URLSearchParams();
    if (query && change.query !== null) search.set('query', query);
    const nextProject = change.project === undefined ? project : change.project;
    if (nextProject) search.set('project', nextProject);
    const text = search.toString();
    return text ? `/articles?${text}` : '/articles';
  };

  return (
    <>
      <PageHeader title="筆記" icon={<NotebookText size={18} strokeWidth={1.8} aria-hidden="true" />}>
        不限程式改動的學習整理，會跟著專案的分享範圍同步到問答。
      </PageHeader>

      {id ? (
        <SingleView backHref="/articles" backLabel="看全部筆記" />
      ) : (
        <>
          <form className="filters" action="/articles" method="get">
            <span className="search-box">
              <Search size={15} aria-hidden="true" />
              <input type="search" name="query" defaultValue={query} placeholder="搜尋筆記，例如 ERP" aria-label="搜尋筆記" />
            </span>
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
          {withArticles.length > 1 && (
            <ChipLinks
              label="依專案篩選"
              items={[
                { href: hrefFor({ project: null }), text: '全部專案', active: !project },
                ...withArticles.map((p) => ({
                  href: hrefFor({ project: p.name }),
                  text: (
                    <span className={`chip-project tone-${projectTone(p.name)}`}>
                      <i aria-hidden="true" />
                      {p.name}
                      <small>{p.article_count}</small>
                    </span>
                  ),
                  active: p.name === project,
                })),
              ]}
            />
          )}
        </>
      )}

      {articles.length === 0 ? (
        id ? (
          <Empty>這篇筆記已經不存在，可能被刪除了。</Empty>
        ) : query || project ? (
          <Empty>沒有符合的筆記。</Empty>
        ) : (
          <NoNotesYet what="筆記" />
        )
      ) : id ? (
        <ArticleFull article={articles[0]!} />
      ) : (
        <div className="entry-list">
          {articles.map((article) => (
            <ArticleRow key={article.id} article={article} />
          ))}
        </div>
      )}
      {!id && <Pager path="/articles" params={{ query, project }} offset={offset} pageSize={PAGE_SIZE} count={count} />}
    </>
  );
}
