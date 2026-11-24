import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { multilineInputHeight } from './textFieldHeight.ts';

describe('altura do campo multilinha', () => {
  it('começa no mínimo e cresce com quebras e texto longo', () => {
    assert.equal(multilineInputHeight(''), 88);
    assert.equal(multilineInputHeight('oi'), 88);
    assert.ok(multilineInputHeight('a\nb\nc\nd\ne\nf') > 88);
    assert.ok(
      multilineInputHeight(
        'Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro\nSegunda linha da captura para o campo crescer no telefone.',
      ) > 88,
    );
  });
});
