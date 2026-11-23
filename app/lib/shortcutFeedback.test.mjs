import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'shortcutFeedback.ts'), 'utf8');

describe('playShortcutFeedback', () => {
  it('usa bip padrão e vibracao curta', () => {
    assert.match(source, /Vibration\.vibrate\(40\)/);
    assert.match(source, /osc\.frequency\.value = 880/);
    assert.match(source, /osc\.stop\(ctx\.currentTime \+ 0\.12\)/);
  });
});
