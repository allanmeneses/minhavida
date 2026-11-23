import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { deviceCalendarPolicy, listDeviceEvents } from './deviceCalendar.ts';

const root = dirname(fileURLToPath(import.meta.url));

describe('calendario do aparelho', () => {
  it('e somente leitura e nunca escreve no calendario nativo', async () => {
    assert.equal(deviceCalendarPolicy.mode, 'read-only');
    assert.equal(deviceCalendarPolicy.writes, false);
    assert.deepEqual(await listDeviceEvents(), []);
    const helper = readFileSync(join(root, 'deviceCalendar.ts'), 'utf8');
    const screen = readFileSync(join(root, '../app/(app)/calendar.tsx'), 'utf8');
    assert.doesNotMatch(helper, /createEventAsync|updateEventAsync|deleteEventAsync|saveEventAsync|createCalendarAsync/);
    assert.doesNotMatch(screen, /createEventAsync|updateEventAsync|deleteEventAsync|saveEventAsync/);
    assert.match(screen, /somente leitura/);
    assert.match(screen, /nunca grava/);
  });

  it('le eventos quando a permissao vem de um leitor injetado', async () => {
    const events = await listDeviceEvents({
      async requestPermission() {
        return true;
      },
      async listEvents() {
        return [{ id: 'cal-1', title: 'Consulta', startDate: '2026-11-23T10:00:00.000Z' }];
      },
    });
    assert.equal(events.length, 1);
    assert.equal(events[0].title, 'Consulta');
  });
});
