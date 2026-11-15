import { ChevronRight, HelpCircle } from 'lucide-react';
import Link from 'next/link';
import type { ReactNode } from 'react';
import type { Article } from '@/lib/api';
import { ConfidenceBadge, ProjectTag } from './ui';

type Block = { kind: 'heading'; text: string } | { kind: 'list'; items: string[] } | { kind: 'text'; text: string };

/**
 * Claude 寫的內文只用到「## 小標」「- 條列」和空行分段，這裡只處理這三種，其他照原文顯示。
 * 不用 Markdown 套件，也就不會把內文裡的 HTML 當成網頁執行。
 */
function toBlocks(body: string): Block[] {
  const blocks: Block[] = [];
  let paragraph: string[] = [];
  const flush = () => {
    if (paragraph.length > 0) blocks.push({ kind: 'text', text: paragraph.join('\n') });
    paragraph = [];
  };
  for (const raw of body.split(/\r?\n/)) {
    const line = raw.trimEnd();
    const heading = /^#{1,4}\s+(.*)$/.exec(line);
    const item = /^\s*[-*・]\s+(.*)$/.exec(line);
    if (heading) {
      flush();
      blocks.push({ kind: 'heading', text: heading[1]! });
    } else if (item) {
      flush();
      const last = blocks[blocks.length - 1];
      if (last?.kind === 'list') last.items.push(item[1]!);
      else blocks.push({ kind: 'list', items: [item[1]!] });
    } else if (line.trim() === '') {
      flush();
    } else {
      paragraph.push(line);
    }
  }
  flush();
  return blocks;
}

export function ArticleBody({ body }: { body: string }) {
  return (
    <div className="article-body">
      {toBlocks(body).map((block, index): ReactNode => {
        if (block.kind === 'heading') return <h3 key={index}>{block.text}</h3>;
        if (block.kind === 'list') {
          return (
            <ul key={index}>
              {block.items.map((item, i) => (
                <li key={i}>{item}</li>
              ))}
            </ul>
          );
        }
        return <p key={index}>{block.text}</p>;
      })}
    </div>
  );
}

const dateFormat = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Taipei' });

export function articleDate(article: Pick<Article, 'updated_at'>): string {
  return dateFormat.format(new Date(article.updated_at));
}

/** 去掉小標和條列符號，當成列表裡的摘要。 */
function excerpt(body: string): string {
  const text = body
    .split(/\r?\n/)
    .filter((line) => !/^#{1,4}\s/.test(line))
    .map((line) => line.replace(/^\s*[-*・]\s+/, ''))
    .join(' ')
    .replace(/\s+/g, ' ')
    .trim();
  return text.length > 110 ? `${text.slice(0, 110)}…` : text;
}

/** 列表裡的一列，點了打開整篇。 */
export function ArticleRow({ article }: { article: Article }) {
  return (
    <Link href={`/articles?id=${article.id}`} className="article-row">
      <time dateTime={article.updated_at}>{articleDate(article)}</time>
      <span className="entry-main">
        <span className="entry-title">{article.title}</span>
        <span className="article-excerpt">{excerpt(article.body)}</span>
        <ProjectTag name={article.project} detail={article.module} />
      </span>
      <ConfidenceBadge confidence={article.confidence} />
      <ChevronRight className="entry-chevron" size={16} aria-hidden="true" />
    </Link>
  );
}

export function ArticleFull({ article }: { article: Article }) {
  return (
    <article className="article" id={`article-${article.id}`}>
      <header className="article-head">
        <h2>{article.title}</h2>
        <p>
          <ProjectTag name={article.project} detail={article.module} />
          <ConfidenceBadge confidence={article.confidence} confirmedBy={article.confirmed_by} />
          <span className="article-date">更新於 {articleDate(article)}</span>
        </p>
      </header>
      <ArticleBody body={article.body} />
      {article.open_questions && (
        <aside className="note">
          <strong>
            <HelpCircle size={13} strokeWidth={1.9} aria-hidden="true" />
            待釐清
          </strong>
          <p>{article.open_questions}</p>
        </aside>
      )}
      {article.concepts.length > 0 && (
        <p className="related">
          <span>相關觀念</span>
          {article.concepts.map((term) => (
            <Link key={term} className="term-chip" href={`/concepts?query=${encodeURIComponent(term)}`}>
              {term}
            </Link>
          ))}
        </p>
      )}
    </article>
  );
}
