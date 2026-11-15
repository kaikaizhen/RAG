import { ArrowLeft, ArrowRight, ChevronRight, ScrollText, Search } from 'lucide-react';
import Link from 'next/link';
import { ModuleNav } from '@/components/ModuleNav';
import { groupRules, RuleAccordion, RuleGroups } from '@/components/RuleGroups';
import { Empty, NoNotesYet, one, PageHeader, projectTone, type SearchParams } from '@/components/ui';
import { listRules, type Rule } from '@/lib/api';
import { splitModule } from '@/lib/modules';

const rulesHref = (project?: string, module?: string) => {
  const query = new URLSearchParams();
  if (project) query.set('project', project);
  if (module) query.set('module', module);
  const text = query.toString();
  return text ? `/rules?${text}` : '/rules';
};

const unconfirmed = (rules: Rule[]) => rules.filter((rule) => rule.confidence === 'code_inferred').length;

function SearchForm({ project, query }: { project?: string; query?: string }) {
  return (
    <form className="filters" action="/rules" method="get">
      {project && <input type="hidden" name="project" value={project} />}
      <span className="search-box">
        <Search size={15} aria-hidden="true" />
        <input type="search" name="query" defaultValue={query} placeholder={project ? `搜尋 ${project} 的規則` : '搜尋所有規則'} aria-label="搜尋規則" />
      </span>
      <button type="submit" className="ghost-button">
        搜尋
      </button>
      {query && (
        <Link href={rulesHref(project)} className="clear-filters">
          清除搜尋
        </Link>
      )}
    </form>
  );
}

/** 第一層：先選專案，不會一進來就看到所有專案的所有規則。 */
function ProjectIndex({ rules }: { rules: Rule[] }) {
  const projects = new Map<string, Rule[]>();
  for (const rule of rules) projects.set(rule.project, [...(projects.get(rule.project) ?? []), rule]);

  return (
    <div className="rule-projects">
      {[...projects].map(([name, items]) => {
        const left = unconfirmed(items);
        return (
          <Link key={name} href={rulesHref(name)} className={`rule-project tone-${projectTone(name)}`}>
            <i aria-hidden="true" />
            <strong>{name}</strong>
            <span>
              {new Set(items.map((rule) => rule.module)).size} 個模組 · {items.length} 條規則
            </span>
            {left > 0 && <small>{left} 條尚未確認</small>}
          </Link>
        );
      })}
    </div>
  );
}

/** 第二層：左邊選模組，右邊一次只看一個模組。 */
function ProjectRules({ project, rules, requested }: { project: string; rules: Rule[]; requested?: string }) {
  // 同一個系統的模組排在一起，上一個／下一個才會在系統內前後移動
  const order = (name: string) => splitModule(name).system ?? '￿';
  const groups = groupRules(rules).sort((a, b) => order(a.module).localeCompare(order(b.module), 'zh-Hant'));
  const index = Math.max(groups.findIndex((group) => group.module === requested), 0);
  const current = groups[index]!;
  const previous = groups[index - 1];
  const next = groups[index + 1];

  return (
    <div className="rules-browser">
      <ModuleNav
        active={current.module}
        modules={groups.map((group) => ({
          name: group.module,
          count: group.items.length,
          unconfirmed: unconfirmed(group.items),
          href: rulesHref(project, group.module),
        }))}
      />
      <section className="rules-detail" key={current.module}>
        {splitModule(current.module).system && <p className="rules-system">{splitModule(current.module).system}</p>}
        <h2>{splitModule(current.module).label}</h2>
        <RuleAccordion rules={current.items} />
        {(previous || next) && (
          <nav className="module-pager" aria-label="上一個或下一個模組">
            {previous ? (
              <Link href={rulesHref(project, previous.module)}>
                <ArrowLeft size={14} aria-hidden="true" />
                {previous.module}
              </Link>
            ) : (
              <span />
            )}
            {next && (
              <Link href={rulesHref(project, next.module)}>
                {next.module}
                <ArrowRight size={14} aria-hidden="true" />
              </Link>
            )}
          </nav>
        )}
      </section>
    </div>
  );
}

export default async function RulesPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const project = one(params.project);
  const query = one(params.query)?.trim() || undefined;
  const { rules } = await listRules({ project, query });

  return (
    <>
      <PageHeader title="規則" icon={<ScrollText size={18} strokeWidth={1.8} aria-hidden="true" />}>
        標示「程式碼推測」的內容尚未確認，引用前要先問過人。
      </PageHeader>

      {project && (
        <nav className="crumbs" aria-label="目前位置">
          <Link href="/rules">所有專案</Link>
          <ChevronRight size={14} aria-hidden="true" />
          <span aria-current="page">{project}</span>
        </nav>
      )}
      <SearchForm project={project} query={query} />

      {rules.length === 0 ? (
        query ? <Empty>沒有符合的規則。</Empty> : project ? <Empty>這個專案還沒有規則。</Empty> : <NoNotesYet what="規則" />
      ) : query ? (
        <RuleGroups rules={rules} />
      ) : project ? (
        <ProjectRules project={project} rules={rules} requested={one(params.module)} />
      ) : (
        <ProjectIndex rules={rules} />
      )}
    </>
  );
}
