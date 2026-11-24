export type DayCell = {
  date: string;
  inMonth: boolean;
  events: { id: string; title: string; paused: boolean }[];
};

export type CalendarEventLike = {
  reminderId: string;
  title: string;
  at: string;
  paused: boolean;
};

export function monthKey(year: number, month: number): string {
  return `${year}-${String(month).padStart(2, '0')}`;
}

export function shiftMonth(year: number, month: number, delta: number): { year: number; month: number } {
  const date = new Date(Date.UTC(year, month - 1 + delta, 1));
  return { year: date.getUTCFullYear(), month: date.getUTCMonth() + 1 };
}

export function buildRecordMonth(
  year: number,
  month: number,
  records: { id: string; title: string; occurredOn: string }[],
): DayCell[] {
  return buildMonth(
    year,
    month,
    records.map((item) => ({
      reminderId: item.id,
      title: item.title,
      at: item.occurredOn,
      paused: false,
    })),
  );
}

export function buildMonth(year: number, month: number, events: CalendarEventLike[]): DayCell[] {
  const first = new Date(Date.UTC(year, month - 1, 1));
  const startWeekday = first.getUTCDay();
  const daysInMonth = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const cells: DayCell[] = [];
  for (let i = 0; i < startWeekday; i++) {
    cells.push({ date: '', inMonth: false, events: [] });
  }
  for (let day = 1; day <= daysInMonth; day++) {
    const date = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    cells.push({
      date,
      inMonth: true,
      events: events
        .filter((item) => item.at.slice(0, 10) === date)
        .map((item) => ({ id: `${item.reminderId}-${item.at}`, title: item.title, paused: item.paused })),
    });
  }
  return cells;
}
