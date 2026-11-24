import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatStorage, storageUsageLine } from './storageDisplay.ts';

describe('armazenamento na tela', () => {
  it('arquivo pequeno não vira zero KB', () => {
    assert.equal(formatStorage(34), '34 B');
    assert.equal(formatStorage(0), '0 B');
    assert.match(formatStorage(1536), /KB/);
    assert.doesNotMatch(storageUsageLine(34, 100 * 1024 * 1024), /^0 KB/);
    assert.match(storageUsageLine(34, 100 * 1024 * 1024), /34 B/);
    assert.match(storageUsageLine(34, 100 * 1024 * 1024), /100 MB/);
  });
});
