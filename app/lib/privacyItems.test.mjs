import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { privacyItemKey, uniquePrivacyItems } from './privacyItems.ts';

describe('itens de privacidade', () => {
  it('não repete a mesma chave e some com o círculo duplicado', () => {
    const items = [
      { what: 'Registros da sua vida', who: 'Só você.' },
      { what: 'Círculo Família', who: 'Só quem está neste círculo.' },
      { what: 'Círculo Família', who: 'Só quem está neste círculo.' },
    ];
    const unique = uniquePrivacyItems(items);
    assert.equal(unique.length, 2);
    assert.equal(unique[1].what, 'Círculo Família');
    const keys = unique.map((item, index) => privacyItemKey(item, index));
    assert.equal(new Set(keys).size, keys.length);
    assert.notEqual(privacyItemKey(items[1], 1), privacyItemKey(items[2], 2));
  });
});
