import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { FAQ_ITEMS } from './faq.ts';

const root = dirname(fileURLToPath(import.meta.url));

describe('faq', () => {
  it('cobre perfil, círculo, offline, sessão e backup em português', () => {
    assert.ok(FAQ_ITEMS.length >= 8);
    const blob = FAQ_ITEMS.map((item) => `${item.question} ${item.answer}`).join(' ');
    assert.match(blob, /perfil solo/);
    assert.match(blob, /círculos/);
    assert.match(blob, /sem internet/);
    assert.match(blob, /renovação da sessão/);
    assert.match(blob, /backup/);
    assert.match(blob, /calendário/);
    assert.match(blob, /memórias/);
    assert.match(blob, /resumo/);
    assert.match(blob, /perfil nasce privado/);
    assert.match(blob, /Só por e-mail/);
    assert.match(blob, /29,90/);
    assert.match(blob, /299,00/);
    assert.equal(
      FAQ_ITEMS.some((item) => item.question.toLowerCase().includes('busca')),
      false,
    );
    assert.doesNotMatch(blob, /\bhub\b/i);
    assert.doesNotMatch(readFileSync(join(root, '../app/(auth)/login.tsx'), 'utf8'), /\bhub\b/i);
    assert.doesNotMatch(readFileSync(join(root, '../app/termos.tsx'), 'utf8'), /\bhub\b/i);
  });
});
