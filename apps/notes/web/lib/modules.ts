/**
 * 模組名稱裡的系統：「預約系統 03 預約共用規則」或「預約系統／預約共用規則」都算屬於「預約系統」。
 * 沒有這種格式的模組（例如 BTOOption）回傳 system: null，畫面上歸在「其他」。
 */
export function splitModule(name: string): { system: string | null; label: string } {
  const slash = /^(.+?)\s*[／/]\s*(.+)$/.exec(name);
  if (slash) return { system: slash[1]!.trim(), label: slash[2]!.trim() };
  const numbered = /^(.+?)\s+(\d{1,3}\s+.+)$/.exec(name);
  if (numbered) return { system: numbered[1]!.trim(), label: numbered[2]!.trim() };
  return { system: null, label: name };
}

export const OTHER_SYSTEM = '其他';

/** 依系統分組，保留原本的順序；只有一個系統都沒有時不分組。 */
export function groupBySystem<T extends { name: string }>(modules: T[]) {
  const groups = new Map<string, Array<T & { label: string }>>();
  for (const module of modules) {
    const { system, label } = splitModule(module.name);
    const key = system ?? OTHER_SYSTEM;
    groups.set(key, [...(groups.get(key) ?? []), { ...module, label }]);
  }
  const named = [...groups].filter(([system]) => system !== OTHER_SYSTEM);
  if (named.length === 0) return null;
  const other = groups.get(OTHER_SYSTEM);
  return [...named, ...(other ? ([[OTHER_SYSTEM, other]] as const) : [])].map(([system, items]) => ({ system, items }));
}
