import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { FAQ_ITEMS } from './faq.ts';

describe('faq', () => {
  it('cobre perfil, circulo, offline, sessao e backup em portugues', () => {
    assert.ok(FAQ_ITEMS.length >= 8);
    const blob = FAQ_ITEMS.map((item) => `${item.question} ${item.answer}`).join(' ');
    assert.match(blob, /perfil solo/);
    assert.match(blob, /circulos/);
    assert.match(blob, /sem internet/);
    assert.match(blob, /refresh/);
    assert.match(blob, /backup/);
    assert.match(blob, /calendario/);
    assert.match(blob, /memorias/);
    assert.match(blob, /recap/);
    assert.match(blob, /perfil nasce privado/);
    assert.match(blob, /So por e-mail/);
    assert.match(blob, /29,90/);
    assert.match(blob, /299,00/);
    assert.equal(
      FAQ_ITEMS.some((item) => item.question.toLowerCase().includes('busca')),
      false,
    );
  });
});
