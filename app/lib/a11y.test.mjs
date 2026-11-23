import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('leitor de tela', () => {
  it('botao e campo tem papel e rotulo', () => {
    const button = readFileSync(join(root, '../components/ui/Button.tsx'), 'utf8');
    const field = readFileSync(join(root, '../components/ui/TextField.tsx'), 'utf8');
    assert.match(button, /accessibilityRole="button"/);
    assert.match(button, /accessibilityLabel=\{label\}/);
    assert.match(field, /accessibilityLabel=\{label\}/);
  });
});
