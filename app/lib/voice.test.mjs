import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { clipFromFile } from './voice.ts';

describe('voz', () => {
  it('aceita um arquivo de teste e preserva o nome', () => {
    const file = new File(['Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro'], 'teste-voz.txt', {
      type: 'text/plain',
    });
    const clip = clipFromFile(file);
    assert.equal(clip.fileName, 'teste-voz.txt');
    assert.equal(clip.mimeType, 'text/plain');
    assert.ok(clip.blob);
  });
});
