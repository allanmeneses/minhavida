import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { applyColorSchemeSafe, colorSchemeValue, webColorScheme } from './colorSchemeCore.ts';

describe('tema escuro sem setColorScheme', () => {
  it('não quebra quando a API nativa não existe', () => {
    assert.equal(colorSchemeValue('dark'), 'dark');
    assert.equal(colorSchemeValue('system'), 'unspecified');
    assert.equal(webColorScheme('dark'), 'dark');
    assert.equal(applyColorSchemeSafe(undefined, 'dark'), false);
    const calls = [];
    assert.equal(
      applyColorSchemeSafe((value) => {
        calls.push(value);
      }, 'dark'),
      true,
    );
    assert.deepEqual(calls, ['dark']);
  });
});
