import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('grade do mês', () => {
  it('cabe o mês inteiro com células compactas', () => {
    const source = readFileSync(join(root, '../components/MonthGrid.tsx'), 'utf8');
    assert.match(source, /minHeight: 36/);
    assert.match(source, /width: '14.285%'/);
    assert.doesNotMatch(source, /minHeight: 64/);
  });
});
