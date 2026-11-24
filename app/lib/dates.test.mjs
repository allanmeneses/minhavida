import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  datePlaceholder,
  formatPersonDateTime,
  isoToDisplayDate,
  monthTitle,
  tagsPlaceholder,
  toIsoDate,
} from './dates.ts';

describe('datas amigáveis', () => {
  it('converte e formata em pt-BR sem AAAA-MM-DD na tela', () => {
    assert.equal(toIsoDate('24/11/2026'), '2026-11-24');
    assert.equal(isoToDisplayDate('2026-11-24'), '24/11/2026');
    assert.equal(datePlaceholder(), 'ex.: 24/11/2026');
    assert.match(tagsPlaceholder(), /palavras-chave/);
    assert.doesNotMatch(datePlaceholder(), /AAAA-MM-DD/);
    assert.equal(monthTitle(2026, 11), 'Novembro de 2026');
    assert.match(formatPersonDateTime('2026-03-10T15:30:00.000Z'), /\d{2}\/\d{2}\/\d{4} \d{2}:\d{2}/);
    assert.doesNotMatch(formatPersonDateTime('2026-03-10T15:30:45.000Z'), /\d{2}:\d{2}:\d{2}/);
  });
});
