import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { webcrypto } from 'node:crypto';
import { totpCode } from './totp.ts';

if (!globalThis.crypto) {
  globalThis.crypto = webcrypto;
}

describe('totp', () => {
  it('bate no vetor RFC 6238 de 6 digitos no segundo 59', async () => {
    const secret = 'GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ';
    const code = await totpCode(secret, 59_000);
    assert.equal(code, '287082');
  });
});
