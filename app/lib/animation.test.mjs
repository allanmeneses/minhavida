import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('transição instantanea', () => {
  it('stack e abas desligam animacao', () => {
    const stack = readFileSync(join(root, '../app/_layout.tsx'), 'utf8');
    const tabs = readFileSync(join(root, '../app/(app)/_layout.tsx'), 'utf8');
    assert.match(stack, /animation: 'none'/);
    assert.match(tabs, /animation: 'none'/);
  });
});
