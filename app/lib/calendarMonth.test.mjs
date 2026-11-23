import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { buildMonth, shiftMonth } from './calendarMonth.ts';

describe('calendário do mês', () => {
  it('mantem o mês escolhido quando só a identidade da função muda', () => {
    const selected = { year: 2026, month: 11 };
    const events = [{ reminderId: 'a', title: 'Tomei o remédio', at: '2026-11-02T08:00:00Z', paused: false }];
    const first = buildMonth(selected.year, selected.month, events);
    const afterRerender = buildMonth(selected.year, selected.month, events);
    assert.equal(first.find((day) => day.date === '2026-11-02')?.events[0]?.title, 'Tomei o remédio');
    assert.equal(afterRerender.find((day) => day.date === '2026-11-02')?.events.length, 1);
    const next = shiftMonth(selected.year, selected.month, 1);
    assert.deepEqual(next, { year: 2026, month: 12 });
  });
});
