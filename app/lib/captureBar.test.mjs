import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('barra de captura', () => {
  it('ações da sugestão vêm antes dos campos, com espaço entre Sugerir e Falar', () => {
    const source = readFileSync(join(root, '../components/CaptureBar.tsx'), 'utf8');
    const aceitar = source.indexOf('label="Aceitar"');
    const editar = source.indexOf('label="Editar"');
    const recusar = source.indexOf('label="Recusar"');
    const fields = source.indexOf('editingSuggestion');
    assert.ok(aceitar > 0);
    assert.ok(editar > aceitar);
    assert.ok(recusar > editar);
    assert.ok(source.indexOf('suggestion.type.fields') > recusar);
    assert.match(source, /styles\.actions/);
    assert.match(source, /gap: 12/);
    assert.match(source, /setAudioName\(null\)/);
    assert.match(source, /!suggestion && voiceState !== 'transcribing'/);
    assert.ok(fields > 0);
  });
});
