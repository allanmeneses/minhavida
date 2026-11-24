import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatPersonTime } from './formatPersonTime.ts';

describe('formatPersonTime', () => {
  it('mostra data e hora no fuso da pessoa', () => {
    const text = formatPersonTime('2026-03-10T15:30:00.000Z');
    assert.match(text, /\d{2}\/\d{2}\/\d{4} \d{2}:\d{2}/);
    assert.notEqual(text, '2026-03-10T15:30:00.000Z');
  });
});
