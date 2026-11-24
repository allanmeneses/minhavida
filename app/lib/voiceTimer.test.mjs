import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('timer da gravação na tela', () => {
  it('mostra Gravando uma vez, com testID único', () => {
    const source = readFileSync(join(root, '../components/CaptureBar.tsx'), 'utf8');
    assert.match(source, /testID="voice-timer"/);
    assert.equal(source.split('recordingLabel(elapsed)').length - 1, 1);
    assert.doesNotMatch(source, /formatElapsed\(elapsed\)/);
    assert.match(source, /Novo tipo/);
    assert.match(source, /setElapsed\(0\)/);
    assert.match(source, /, 1000\)/);
  });
});
