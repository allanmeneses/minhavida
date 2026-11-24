import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('seletor de arquivo', () => {
  it('usa botão em pt-BR e esconde o seletor nativo em inglês', () => {
    const source = readFileSync(join(root, '../components/ui/FilePicker.tsx'), 'utf8');
    assert.match(source, /Escolher arquivo/);
    assert.match(source, /Nenhum arquivo escolhido/);
    assert.doesNotMatch(source, /Choose File/);
    assert.doesNotMatch(source, /No file chosen/);
    assert.match(source, /clipPath/);
    assert.match(source, /opacity: 0/);
  });
});
