'use client';

import { Search } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';
import { groupBySystem, splitModule } from '@/lib/modules';

export interface ModuleLink {
  name: string;
  count: number;
  unconfirmed: number;
  href: string;
}

function ModuleItem({ module, label, active }: { module: ModuleLink; label: string; active: boolean }) {
  return (
    <Link href={module.href} className="module-link" aria-current={active ? 'page' : undefined} title={module.name}>
      <span>{label}</span>
      {module.unconfirmed > 0 && <i title={`${module.unconfirmed} 條尚未確認`} aria-label={`${module.unconfirmed} 條尚未確認`} />}
      <b>{module.count}</b>
    </Link>
  );
}

/** 模組依系統分組、可收合；模組多的專案（例如一個系統幾十支程式）也可以直接打字找。 */
export function ModuleNav({ modules, active }: { modules: ModuleLink[]; active: string }) {
  const [filter, setFilter] = useState('');
  const keyword = filter.trim().toLowerCase();
  const shown = keyword ? modules.filter((module) => module.name.toLowerCase().includes(keyword)) : modules;
  const groups = groupBySystem(shown);
  const activeSystem = splitModule(active).system;

  return (
    <nav className="module-nav" aria-label="模組">
      {modules.length > 6 && (
        <label className="module-filter">
          <Search size={14} aria-hidden="true" />
          <input value={filter} onChange={(event) => setFilter(event.target.value)} placeholder={`在 ${modules.length} 個模組裡找`} aria-label="篩選模組" />
        </label>
      )}
      <div className="module-links">
        {groups
          ? groups.map(({ system, items }) => (
              // 篩選時全部展開，平常只展開目前所在的系統
              <details key={`${system}-${keyword ? 'filter' : 'normal'}`} className="module-system" open={Boolean(keyword) || system === (activeSystem ?? '其他')}>
                <summary>
                  <span>{system}</span>
                  <b>{items.length}</b>
                </summary>
                {items.map((module) => (
                  <ModuleItem key={module.name} module={module} label={module.label} active={module.name === active} />
                ))}
              </details>
            ))
          : shown.map((module) => <ModuleItem key={module.name} module={module} label={module.name} active={module.name === active} />)}
        {shown.length === 0 && <p className="module-empty">沒有符合的模組</p>}
      </div>
    </nav>
  );
}
