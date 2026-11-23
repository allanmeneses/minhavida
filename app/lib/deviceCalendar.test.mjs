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
});
