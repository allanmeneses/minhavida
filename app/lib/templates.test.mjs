import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

describe('modelos', () => {
  it('catalogo inicial esta em português e é anônimo', () => {
    const text = readFileSync(resolve(root, 'backend/src/Circulos.Api/Services/TemplateCatalog.cs'), 'utf8');
    assert.match(text, /Compras da semana/);
    assert.match(text, /Tomei o remédio/);
    assert.match(text, /Cuidados do dia/);
    assert.match(text, /Corrida da semana/);
    assert.doesNotMatch(text, /creatorName/);
  });
});
