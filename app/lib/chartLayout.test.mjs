import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { chartBars, chartLine, chartScale, MAX_BAR_WIDTH, SINGLE_POINT_HINT } from './chartLayout.ts';

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
    assert.equal(bars[0].label, '10/11/2026');
    assert.equal(bars[0].valueLabel, '12');
    const fromDateOnly = chartBars([{ occurredOn: '2026-11-10', numberValue: 12 }]);
    assert.equal(fromDateOnly[0].label, '10/11/2026');
    assert.ok(bars[0].height >= 4);
    assert.ok(bars[0].width <= MAX_BAR_WIDTH);
    assert.match(SINGLE_POINT_HINT, /registre mais/);
  });

  it('eixo vem dos dados e 11,8 não vira 118', () => {
    const points = [
      { occurredOn: '2026-01-01', numberValue: 11.8 },
      { occurredOn: '2026-02-01', numberValue: 12 },
    ];
    const { min, max } = chartScale(points);
    assert.ok(min > 10);
    assert.ok(max < 20);
    assert.ok(min < 11.8);
    assert.ok(max > 12);
    const line = chartLine(points, 200, 100);
    const ys = line.points.split(' ').map((pair) => Number(pair.split(',')[1]));
    assert.equal(ys.length, 2);
    assert.ok(Number.isFinite(ys[0]));
    assert.ok(Number.isFinite(ys[1]));
    const bars = chartBars(points);
    assert.match(bars[0].valueLabel, /11,8/);
    assert.doesNotMatch(bars[0].valueLabel, /118/);
    assert.equal(bars[1].valueLabel, '12');
  });
});
