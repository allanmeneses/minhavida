import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('salvar e mudo', () => {
  it('memórias e diário não disparam som ao salvar', () => {
    const memories = readFileSync(join(root, '../app/(app)/memories.tsx'), 'utf8');
    const life = readFileSync(join(root, '../app/(app)/life/[id].tsx'), 'utf8');
    assert.doesNotMatch(memories, /playShortcutFeedback/);
    assert.doesNotMatch(life, /playShortcutFeedback/);
    assert.match(memories, /Edicao e silenciosa/);
  });
});
