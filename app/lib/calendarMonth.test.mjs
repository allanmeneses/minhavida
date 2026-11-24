import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { buildMonth, buildRecordMonth, shiftMonth } from './calendarMonth.ts';

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
    const records = buildRecordMonth(2026, 11, [{ id: 'r1', title: 'Gripe', occurredOn: '2026-11-10' }]);
    assert.equal(records.filter((day) => !day.inMonth).length % 7, records.filter((day) => !day.inMonth).length);
    assert.ok(records.length >= 30);
    assert.equal(records.find((day) => day.date === '2026-11-10')?.events[0]?.title, 'Gripe');
  });
});

