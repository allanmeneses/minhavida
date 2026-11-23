import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('lazy load de imagem', () => {
  it('foto na web usa loading lazy', () => {
    const source = readFileSync(join(root, '../components/LazyPhoto.tsx'), 'utf8');
    assert.match(source, /loading="lazy"/);
    const life = readFileSync(join(root, '../app/(app)/life/[id].tsx'), 'utf8');
    assert.match(life, /LazyPhoto/);
  });
});
