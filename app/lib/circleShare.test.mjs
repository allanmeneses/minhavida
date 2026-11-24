import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { shareChipLabel, toggleCircleShare } from './circleShare.ts';

describe('compartilhar com círculos', () => {
  it('liga e desliga o chip sem apagar os outros', () => {
    const eu = 'eu-id';
    const familia = 'fam-id';
    const once = toggleCircleShare([], eu);
    assert.deepEqual(once, [eu]);
    const both = toggleCircleShare(once, familia);
    assert.deepEqual(both, [eu, familia]);
    const again = toggleCircleShare(both, eu);
    assert.deepEqual(again, [familia]);
    assert.match(shareChipLabel('Eu', false), /Compartilhar com Eu/);
    assert.match(shareChipLabel('Família', true), /selecionado/);
    assert.doesNotMatch(shareChipLabel('Eu', false), /Fora do círculo/);
  });
});
