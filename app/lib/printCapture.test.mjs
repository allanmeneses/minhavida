import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('script de prints', () => {
  it('rola topo, meio e fundo, recarrega a rota e usa banco isolado', () => {
    const capture = readFileSync(join(root, '../e2e/printCapture.ts'), 'utf8');
    const printsSpec = readFileSync(join(root, '../e2e/prints.spec.ts'), 'utf8');
    const printsConfig = readFileSync(join(root, '../playwright.prints.config.ts'), 'utf8');
    const mainConfig = readFileSync(join(root, '../playwright.config.ts'), 'utf8');
    assert.match(capture, /scrollInner\(page, 0\)/);
    assert.match(capture, /scrollInner\(page, 0\.5\)/);
    assert.match(capture, /scrollInner\(page, 1\)/);
    assert.match(capture, /-meio\.png/);
    assert.match(capture, /-baixo\.png/);
    assert.match(capture, /Database=circulos_prints/);
    assert.match(capture, /page\.reload/);
    assert.match(printsSpec, /reloadAppRoute/);
    assert.match(printsSpec, /screenshotSteps/);
    assert.match(printsConfig, /Database=circulos_prints/);
    assert.match(printsConfig, /testMatch: 'prints\.spec\.ts'/);
    assert.match(mainConfig, /testIgnore: \['prints\.spec\.ts'/);
  });
});
