const WEEKS = 36;
const DAY_MS = 24 * 60 * 60 * 1000;

function toDay(text: string): number {
  return Date.parse(`${text}T00:00:00Z`);
}

function toText(day: number): string {
  return new Date(day).toISOString().slice(0, 10);
}

function level(count: number): number {
  return Math.min(count, 4);
}

/** 每格一天、每欄一週，顏色越深代表當天的改動紀錄越多。 */
export function ActivityHeatmap({ dates, today }: { dates: string[]; today: string }) {
  const counts = new Map<string, number>();
  for (const date of dates) counts.set(date, (counts.get(date) ?? 0) + 1);

  const end = toDay(today);
  const thisWeekStart = end - new Date(end).getUTCDay() * DAY_MS;
  const start = thisWeekStart - (WEEKS - 1) * 7 * DAY_MS;

  const cells: { date: string; column: number; row: number; count: number }[] = [];
  for (let day = start; day <= end; day += DAY_MS) {
    const date = toText(day);
    cells.push({
      date,
      column: Math.floor((day - start) / (7 * DAY_MS)) + 1,
      row: new Date(day).getUTCDay() + 2,
      count: counts.get(date) ?? 0,
    });
  }

  const months: { label: string; column: number }[] = [];
  let previous = -1;
  for (const cell of cells) {
    const month = Number(cell.date.slice(5, 7));
    // 剛好在週日才標月份，文字才會對齊那一欄
    if (cell.row === 2 && month !== previous) {
      months.push({ label: `${month} 月`, column: cell.column });
      previous = month;
    }
  }

  const total = cells.reduce((sum, cell) => sum + cell.count, 0);
  const active = cells.filter((cell) => cell.count > 0).length;

  return (
    <div className="heatmap-wrap">
      <div className="heatmap" style={{ gridTemplateColumns: `repeat(${WEEKS}, 12px)` }} role="img" aria-label={`近 ${WEEKS} 週有 ${active} 天留下紀錄，共 ${total} 筆`}>
        {months.map((month) => (
          <span key={month.column} className="heatmap-month" style={{ gridColumn: `${month.column} / span 4`, gridRow: 1 }}>
            {month.label}
          </span>
        ))}
        {cells.map((cell) => (
          <i
            key={cell.date}
            className={`heat-${level(cell.count)}`}
            style={{ gridColumn: cell.column, gridRow: cell.row }}
            title={`${cell.date}：${cell.count} 筆`}
          />
        ))}
      </div>
      <div className="heatmap-legend" aria-hidden="true">
        少
        {[0, 1, 2, 3, 4].map((value) => (
          <i key={value} className={`heat-${value}`} />
        ))}
        多
      </div>
    </div>
  );
}
