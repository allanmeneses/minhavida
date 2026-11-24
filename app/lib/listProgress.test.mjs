import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { listProgressLabel } from './listProgress.ts';

describe('progresso da lista', () => {
  it('mostra feitos e total mesmo com itens em aberto', () => {
    assert.equal(listProgressLabel(0, 2), '0 de 2 feitos');
    assert.equal(listProgressLabel(1, 2), '1 de 2 feitos');
    assert.equal(listProgressLabel(2, 2), '2 de 2 feitos');
  });
});
