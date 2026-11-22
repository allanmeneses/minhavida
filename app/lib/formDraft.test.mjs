import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { hydrateIfPristine } from './formDraft.ts';

describe('hydrateIfPristine', () => {
  it('keeps typed text when the parent only swaps the save function identity', () => {
    const typed = { displayName: 'texto que a pessoa digitou' };
    const incoming = { displayName: 'Ana' };
    const afterRerender = hydrateIfPristine(true, typed, incoming);
    assert.equal(afterRerender.displayName, 'texto que a pessoa digitou');
  });

  it('loads server values while the form is still empty', () => {
    const empty = { displayName: '' };
    const incoming = { displayName: 'Ana' };
    const afterLoad = hydrateIfPristine(false, empty, incoming);
    assert.equal(afterLoad.displayName, 'Ana');
  });
});
