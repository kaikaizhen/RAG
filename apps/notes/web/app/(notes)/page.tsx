import { BookOpen, History, House, NotebookText, ScrollText } from 'lucide-react';
import Link from 'next/link';
import { randomInt } from 'node:crypto';
import { ActivityHeatmap } from '@/components/ActivityHeatmap';
import { articleDate, ArticleRow } from '@/components/ArticleView';
import { EntryItem } from '@/components/EntryItem';
import { FlashCards } from '@/components/FlashCards';
import { Onboarding } from '@/components/Onboarding';
import { Empty, NoNotesYet, PageHeader, projectBreakdown, projectHref, projectSize, projectTone } from '@/components/ui';
import { getStats, listArticles, listConcepts, listEntries, listProjects, listRules, type Article, type Entry } from '@/lib/api';

type Recent = { date: string; entry: Entry; article?: never } | { date: string; article: Article; entry?: never };

const DAY_MS = 24 * 60 * 60 * 1000;
const WEEKDAYS = ['週日', '週一', '週二', '週三', '週四', '週五', '週六'];

export default async function OverviewPage() {
  const [stats, { entries }, { articles }, { concepts }, { concepts: questions }, { projects }, { rules: inferredRules }] = await Promise.all([
    getStats(),
    listEntries({ limit: 50 }),
    listArticles({ limit: 50 }),
    listConcepts({ limit: 50 }),
    listConcepts({ has_open_questions: true, limit: 3 }),
    listProjects(),
    listRules({ confidence: 'code_inferred' }),
  ]);

  if (stats.entries + stats.articles + stats.concepts + stats.rules === 0) {
    return (
      <>
        <PageHeader title="歡迎" icon={<House size={18} strokeWidth={1.8} aria-hidden="true" />}>
          這本筆記只有你看得到。Claude 幫你寫，你隨時回來翻。
        </PageHeader>
        <Onboarding />
      </>
    );
  }

  const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Taipei' }).format(new Date());
  const todayMs = Date.parse(`${today}T00:00:00Z`);
  const weekday = WEEKDAYS[new Date(todayMs).getUTCDay()];
  const within30Days = (date: string) => todayMs - Date.parse(`${date}T00:00:00Z`) < 30 * DAY_MS;
  const recentCount = entries.filter((entry) => within30Days(entry.happened_on)).length;
  const recentArticles = articles.filter((article) => within30Days(articleDate(article))).length;

  // 改動紀錄看發生日，筆記看最後更新日；同一天筆記排前面，剛寫的東西比較容易看到
  const recent: Recent[] = [
    ...articles.map((article) => ({ date: articleDate(article), article })),
    ...entries.map((entry) => ({ date: entry.happened_on, entry })),
  ]
    .sort((a, b) => b.date.localeCompare(a.date))
    .slice(0, 6);

  const total = stats.general_definition + stats.code_inferred + stats.colleague_confirmed;
  const percent = (value: number) => (total === 0 ? 0 : (value / total) * 100);
  const sources = [
    { key: 'confirmed', label: '已向同事確認', value: stats.colleague_confirmed },
    { key: 'general', label: '一般定義', value: stats.general_definition },
    { key: 'inferred', label: '程式碼推測', value: stats.code_inferred },
  ];

  const tiles = [
    { href: '/entries', label: '改動紀錄', value: stats.entries, note: `近 30 天 ${recentCount} 筆`, icon: History },
    { href: '/articles', label: '筆記', value: stats.articles, note: `近 30 天 ${recentArticles} 篇`, icon: NotebookText },
    { href: '/concepts', label: '觀念', value: stats.concepts, note: `${stats.open_questions} 個待釐清`, icon: BookOpen },
    { href: '/rules', label: '現行規則', value: stats.rules, note: `${inferredRules.length} 條尚未確認`, icon: ScrollText },
  ];

  const topProjects = projects
    .filter((project) => projectSize(project) > 0)
    .sort((a, b) => projectSize(b) - projectSize(a))
    .slice(0, 5);
  const maxProject = topProjects[0] ? projectSize(topProjects[0]) : 1;

  const cards = concepts.map(({ id, term, plain_explanation, confidence, confirmed_by }) => ({
    id,
    term,
    plain_explanation,
    confidence,
    confirmed_by,
  }));

  return (
    <>
      <PageHeader title="總覽" icon={<House size={18} strokeWidth={1.8} aria-hidden="true" />}>
        {today}　{weekday}
      </PageHeader>

      <div className="stats">
        {tiles.map(({ href, label, value, note, icon: Icon }) => (
          <Link key={label} href={href} className="stat">
            <span className="stat-label">
              {label}
              <Icon size={15} strokeWidth={1.8} aria-hidden="true" />
            </span>
            <strong>{value}</strong>
            <span className="stat-note">{note}</span>
          </Link>
        ))}
      </div>

      <div className="insight-grid">
        <section className="panel">
          <div className="section-head">
            <h2>活動</h2>
            <span className="count">{entries.length === 50 ? '依最近 50 筆紀錄' : `共 ${entries.length} 筆紀錄`}</span>
          </div>
          <ActivityHeatmap dates={entries.map((entry) => entry.happened_on)} today={today} />
        </section>

        <section className="panel">
          <div className="section-head">
            <h2>專案分布</h2>
          </div>
          {topProjects.length === 0 ? (
            <Empty>還沒有內容。</Empty>
          ) : (
            <ul className="project-bars">
              {topProjects.map((project) => (
                <li key={project.name} className={`tone-${projectTone(project.name)}`}>
                  <Link href={projectHref(project)}>
                    <i aria-hidden="true" />
                    {project.name}
                  </Link>
                  <b>{projectSize(project)}</b>
                  <span className="project-bar">
                    <span style={{ width: `${(projectSize(project) / maxProject) * 100}%` }} />
                  </span>
                  <small>{projectBreakdown(project)}</small>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <div className="overview-grid">
        <section>
          <div className="section-head">
            <h2>最近更新</h2>
            <span className="more-links">
              <Link href="/articles" className="more">
                全部筆記
              </Link>
              <Link href="/entries" className="more">
                全部紀錄
              </Link>
            </span>
          </div>
          {recent.length === 0 ? (
            <NoNotesYet what="紀錄或筆記" />
          ) : (
            <div className="entry-list">
              {recent.map((item) =>
                item.article ? (
                  <ArticleRow key={`article-${item.article.id}`} article={item.article} />
                ) : (
                  <EntryItem key={`entry-${item.entry.id}`} entry={item.entry} />
                ),
              )}
            </div>
          )}
        </section>

        <div className="overview-side">
          <section className="panel">
            <div className="section-head">
              <h2>內容來源</h2>
              <span className="count">共 {total} 筆</span>
            </div>
            <div
              className="meter"
              role="img"
              aria-label={sources.map((source) => `${source.label} ${source.value} 筆`).join('，')}
            >
              {sources.map((source) =>
                source.value > 0 ? <span key={source.key} className={`meter-${source.key}`} style={{ width: `${percent(source.value)}%` }} /> : null,
              )}
            </div>
            <ul className="legend">
              {sources.map((source) => (
                <li key={source.key}>
                  <i className={`dot dot-${source.key}`} />
                  {source.label}
                  <b>
                    {source.value}
                    <small>{Math.round(percent(source.value))}%</small>
                  </b>
                </li>
              ))}
            </ul>
          </section>

          {questions.length > 0 && (
            <section className="panel">
              <div className="section-head">
                <h2>待向同事確認</h2>
                <Link href="/questions" className="more">
                  查看全部
                </Link>
              </div>
              <ul className="ask-list">
                {questions.map((concept) => (
                  <li key={concept.id}>
                    <Link href={`/concepts?id=${concept.id}`}>{concept.term}</Link>
                    {concept.open_questions}
                  </li>
                ))}
              </ul>
            </section>
          )}

          <section>
            <div className="section-head">
              <h2>觀念複習</h2>
            </div>
            {cards.length > 0 ? <FlashCards cards={cards} start={randomInt(cards.length)} /> : <Empty>還沒有觀念卡。</Empty>}
          </section>
        </div>
      </div>
    </>
  );
}
