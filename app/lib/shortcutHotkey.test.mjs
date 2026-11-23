import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { isTypingTarget, shortcutIndexFromKey } from './shortcutHotkey.ts';

const root = dirname(fileURLToPath(import.meta.url));

describe('atalho por tecla', () => {
  it('dispara 1 a 9 fora de campo e ignora enquanto a pessoa digita', () => {
    assert.equal(shortcutIndexFromKey('1', false), 0);
    assert.equal(shortcutIndexFromKey('9', false), 8);
    assert.equal(shortcutIndexFromKey('1', true), null);
    assert.equal(shortcutIndexFromKey('a', false), null);
    assert.equal(isTypingTarget({ tagName: 'INPUT' }), true);
    assert.equal(isTypingTarget({ tagName: 'DIV' }), false);
    const home = readFileSync(join(root, '../app/(app)/home.tsx'), 'utf8');
    assert.match(home, /shortcutIndexFromKey/);
    assert.doesNotMatch(home, /Accelerometer|DeviceMotion|shake/i);
  });
});
