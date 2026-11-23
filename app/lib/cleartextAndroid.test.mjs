import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

describe('HTTP em texto puro no Android', () => {
  it('não libera HTTP na configuração de produção do app.json', () => {
    const app = JSON.parse(readFileSync(join(root, 'app.json'), 'utf8'));
    assert.notEqual(app.expo.android.usesCleartextTraffic, true);
    assert.ok(app.expo.plugins.includes('./plugins/withCleartextDebugOnly'));
  });

  it('plugin trava HTTP no release e libera só no debug/emulador', () => {
    const source = readFileSync(join(root, 'plugins/withCleartextDebugOnly.js'), 'utf8');
    assert.match(source, /android:usesCleartextTraffic'] = 'false'/);
    assert.match(source, /android:usesCleartextTraffic="true"/);
    assert.match(source, /app\/src\/debug/);
  });
});
