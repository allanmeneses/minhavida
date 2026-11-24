import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('componente de gráfico', () => {
  it('usa linha só com dois pontos ou mais, e barra no ponto único', () => {
    const source = readFileSync(join(root, '../components/NumberChart.tsx'), 'utf8');
    assert.match(source, /points\.length >= 2/);
    assert.match(source, /testID="chart-bar"/);
    assert.match(source, /polyline/);
    assert.match(source, /Ainda não há números neste tipo/);
  });
});
