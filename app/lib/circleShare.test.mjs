import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { shareChipLabel, toggleCircleShare, uniqueShareCircles } from './circleShare.ts';

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

  it('mostra um chip por nome, mesmo com dois círculos Família', () => {
    const chips = uniqueShareCircles([
      { id: 'eu', name: 'Eu' },
      { id: 'fam-1', name: 'Família' },
      { id: 'fam-1', name: 'Família' },
      { id: 'fam-2', name: 'Família' },
    ]);
    assert.deepEqual(
      chips.map((item) => item.name),
      ['Eu', 'Família'],
    );
    assert.equal(chips.length, 2);
  });
});
