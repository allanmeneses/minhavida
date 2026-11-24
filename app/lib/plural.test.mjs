import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { countNoun, daysPhrase, noun } from './plural.ts';

describe('plural em pt-BR', () => {
  it('usa singular e plural de verdade, sem (s)', () => {
    assert.equal(countNoun(0, 'círculo ativo', 'círculos ativos'), '0 círculos ativos');
    assert.equal(countNoun(1, 'círculo ativo', 'círculos ativos'), '1 círculo ativo');
    assert.equal(countNoun(2, 'círculo ativo', 'círculos ativos'), '2 círculos ativos');
    assert.equal(countNoun(1, 'pessoa', 'pessoas'), '1 pessoa');
    assert.equal(countNoun(3, 'pessoa', 'pessoas'), '3 pessoas');
    assert.equal(daysPhrase(1), '1 dia');
    assert.equal(daysPhrase(3), '3 dias');
    assert.equal(noun(1, 'memória', 'memórias'), 'memória');
    assert.equal(noun(2, 'entrada', 'entradas'), 'entradas');
  });
});
