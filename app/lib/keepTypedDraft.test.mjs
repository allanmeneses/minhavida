import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { keepTypedDraft } from './keepTypedDraft.ts';

describe('rascunho da memoria', () => {
  it('rerender com nova identidade de onSave nao apaga o texto digitado', () => {
    const lastSaved = 'Foto da entrada';
    const onSaveA = () => undefined;
    const onSaveB = () => undefined;
    const typed = 'Texto que a pessoa digitou';
    void onSaveA;
    void onSaveB;
    assert.equal(keepTypedDraft(typed, lastSaved, lastSaved), 'Texto que a pessoa digitou');
    assert.equal(keepTypedDraft(lastSaved, 'veio do servidor', lastSaved), 'veio do servidor');
  });
});
