import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { chartBars, chartLine } from './chartLayout.ts';

describe('gráfico de números', () => {
  it('monta barras e linha a partir dos pontos', () => {
    const points = [
      { occurredOn: '2026-11-01', numberValue: 2 },
      { occurredOn: '2026-11-08', numberValue: 10 },
    ];
    const bars = chartBars(points);
    assert.equal(bars.length, 2);
    assert.ok(bars[1].height > bars[0].height);
    const line = chartLine(points);
    assert.match(line.points, /\d+.*,.*\d+/);
    assert.equal(chartBars([]).length, 0);
  });

  it('um único ponto vira barra visível', () => {
    const bars = chartBars([{ occurredOn: '2026-11-10', numberValue: 12 }]);
    assert.equal(bars.length, 1);
    assert.equal(bars[0].label, '10');
    assert.ok(bars[0].height >= 4);
  });
});
