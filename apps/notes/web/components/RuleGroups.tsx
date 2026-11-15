import type { Rule } from '@/lib/api';
import { ConfidenceBadge, ProjectTag } from './ui';

const COMMAND = /^(docker|cd |sudo |curl |tail |systemctl |crontab |\(crontab|~\/|traefik\.|SELECT )/;

/** 整段都是指令的段落用等寬字呈現，其他維持一般文字。 */
function RuleBody({ body }: { body: string }) {
  const blocks = body.split(/\n{2,}/);
  return (
    <div className="rule-body">
      {blocks.map((block, index) => {
        const isCode = block.split('\n').every((line) => COMMAND.test(line.trim()));
        return isCode ? <pre key={index}>{block}</pre> : <p key={index}>{block}</p>;
      })}
    </div>
  );
}

export function groupRules(rules: Rule[]) {
  const groups = new Map<string, { project: string; module: string; items: Rule[] }>();
  for (const rule of rules) {
    const key = `${rule.project}/${rule.module}`;
    const group = groups.get(key) ?? { project: rule.project, module: rule.module, items: [] };
    group.items.push(rule);
    groups.set(key, group);
  }
  return [...groups.values()];
}

/** 同一個模組的規則，預設收合只看標題；規則少的時候才全部展開，長內容不會把頁面撐滿。 */
export function RuleAccordion({ rules }: { rules: Rule[] }) {
  const open = rules.length <= 3;
  return (
    <div className="rule-accordion">
      {rules.map((rule) => (
        <details key={rule.id} className="rule-item" open={open}>
          <summary>
            <span className="rule-title">{rule.title}</span>
            <ConfidenceBadge confidence={rule.confidence} confirmedBy={rule.confirmed_by} />
          </summary>
          <RuleBody body={rule.body} />
        </details>
      ))}
    </div>
  );
}

/** 依「專案 / 模組」分組，給待確認清單和搜尋結果這種跨模組的列表用。 */
export function RuleGroups({ rules }: { rules: Rule[] }) {
  return (
    <>
      {groupRules(rules).map((group) => (
        <section key={`${group.project}/${group.module}`} className="rule-group">
          <h2>
            <ProjectTag name={group.project} />
            <span className="rule-module">{group.module}</span>
            <span className="count">{group.items.length}</span>
          </h2>
          <RuleAccordion rules={group.items} />
        </section>
      ))}
    </>
  );
}
