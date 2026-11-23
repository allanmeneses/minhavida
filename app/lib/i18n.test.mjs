import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
const root = dirname(fileURLToPath(import.meta.url));

describe('i18n', () => {
  it('carrega chaves em portugues a partir de arquivo', () => {
    const catalog = JSON.parse(readFileSync(join(root, '../locales/pt-BR.json'), 'utf8'));
    assert.equal(catalog['home.title'], 'O que pede atencao hoje');
    assert.equal(catalog['home.onThisDay'], 'Neste dia');
    assert.equal(catalog['privacy.title'], 'Quem ve o que');
    assert.match(readFileSync(join(root, 'i18n.ts'), 'utf8'), /locales\/pt-BR\.json/);
  });
});
