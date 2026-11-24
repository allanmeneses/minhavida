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
    const search = readFileSync(join(root, '../app/(app)/search.tsx'), 'utf8');
    const documents = readFileSync(join(root, '../app/(app)/documents.tsx'), 'utf8');
    assert.match(button, /accessibilityRole="button"/);
    assert.match(button, /accessibilityLabel=\{accessibilityLabel \?\? label\}/);
    assert.match(button, /backgroundColor: variant === 'danger' \? colors.dangerSoft : colors.line/);
    assert.doesNotMatch(button, /disabled: \{\s*opacity: 0\.5/);
    assert.match(field, /accessibilityLabel=\{label\}/);
    assert.match(field, /onContentSizeChange/);
    assert.match(field, /fieldSizing: 'content'/);
    assert.match(search, /accessibilityLabel=\{`Buscar \$\{term\}`\}/);
    assert.match(search, /entry: 'entrada'/);
    assert.match(search, /searchTypeLabel\(hit\.type\)/);
    assert.match(search, /searchTypeLabel\(filter\)/);
    assert.doesNotMatch(search, /Filtro: \$\{filter\}/);
    assert.match(documents, /accessibilityLabel=\{`Baixar \$\{item\.fileName\}`\}/);
    assert.match(documents, /accessibilityLabel=\{`Excluir \$\{item\.fileName\}`\}/);
  });
});
