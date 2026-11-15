import { History, Search } from 'lucide-react';
import Link from 'next/link';
import { EntryGroups } from '@/components/EntryItem';
import { Select } from '@/components/Select';
import { Empty, NoNotesYet, one, offsetFrom, PageHeader, Pager, SingleView, TEST_STATUS_LABEL, type SearchParams } from '@/components/ui';
import { listEntries, listProjects, PAGE_SIZE, type TestStatus } from '@/lib/api';

export default async function EntriesPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const query = one(params.query);
  const project = one(params.project);
  const testStatus = one(params.test_status);
  // ?id= 是外部（例如 RAG 的來源標籤）連進來看單篇用的
  const id = /^\d+$/.test(one(params.id) ?? '') ? one(params.id) : undefined;
  const offset = offsetFrom(params.offset);

  const [{ projects }, { count, entries }] = await Promise.all([
    listProjects(),
    listEntries({ id, query, project, test_status: testStatus, offset }),
  ]);
  const filtered = Boolean(id || query || project || testStatus);

  return (
    <>
      <PageHeader title="改動紀錄" icon={<History size={18} strokeWidth={1.8} aria-hidden="true" />}>
        每次改了什麼、為什麼改。
      </PageHeader>
      <form className="filters" action="/entries" method="get">
        <span className="search-box">
          <Search size={15} aria-hidden="true" />
          <input type="search" name="query" defaultValue={query} placeholder="搜尋關鍵字，空白分隔" aria-label="搜尋關鍵字" />
        </span>
        <Select
          name="project"
          label="專案"
          value={project ?? ''}
          options={[
            { value: '', label: '全部專案' },
            ...projects.map((p) => ({ value: p.name, label: `${p.name}（${p.entry_count}）` })),
          ]}
        />
        <Select
          name="test_status"
          label="測試狀態"
          value={testStatus ?? ''}
          options={[
            { value: '', label: '全部測試狀態' },
            ...(Object.keys(TEST_STATUS_LABEL) as TestStatus[]).map((status) => ({
              value: status,
              label: TEST_STATUS_LABEL[status],
            })),
          ]}
        />
        <button type="submit" className="ghost-button">
          搜尋
        </button>
        {filtered && !id && (
          <Link href="/entries" className="clear-filters">
            清除條件
          </Link>
        )}
      </form>

      {id && <SingleView backHref="/entries" backLabel="看全部紀錄" />}
      {entries.length === 0 ? (
        id || filtered ? (
          <Empty>{id ? '這筆紀錄已經不存在，可能被刪除了。' : '沒有符合條件的紀錄。'}</Empty>
        ) : (
          <NoNotesYet what="紀錄" />
        )
      ) : (
        <EntryGroups entries={entries} open={Boolean(id)} />
      )}
      <Pager path="/entries" params={{ id, query, project, test_status: testStatus }} offset={offset} pageSize={PAGE_SIZE} count={count} />
    </>
  );
}
