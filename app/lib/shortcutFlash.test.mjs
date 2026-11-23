import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { SHORTCUT_FLASH_MS, flashStillVisible } from './shortcutFlash.ts';

describe('feedback do atalho', () => {
  it('some depois de 1 segundo e sobrevive a um rerender no meio', () => {
    assert.equal(SHORTCUT_FLASH_MS, 1000);
    const startedAt = 10_000;
    assert.equal(flashStillVisible(startedAt, startedAt + 999), true);
    assert.equal(flashStillVisible(startedAt, startedAt + 1000), false);
    const afterParentRerender = startedAt + 400;
    assert.equal(flashStillVisible(startedAt, afterParentRerender), true);
  });
});
