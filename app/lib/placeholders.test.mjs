import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { examplePlaceholder } from './placeholders.ts';

describe('placeholder de exemplo', () => {
  it('prefixa ex.: e não duplica', () => {
    assert.equal(examplePlaceholder('Hepatite B'), 'ex.: Hepatite B');
    assert.equal(examplePlaceholder('ex.: UBS Centro'), 'ex.: UBS Centro');
    assert.equal(examplePlaceholder('11,8'), 'ex.: 11,8');
    assert.equal(examplePlaceholder(''), '');
  });
});
