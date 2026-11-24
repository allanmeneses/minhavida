import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatElapsed, recordingLabel, transcribingLabel } from './voiceUi.ts';
import { startTestRecording } from './voice.ts';

describe('estados da voz', () => {
  it('formata timer e rótulos de gravação e transcrição', () => {
    assert.equal(formatElapsed(0), '00:00');
    assert.equal(formatElapsed(75), '01:15');
    assert.equal(recordingLabel(12), 'Gravando 00:12');
    assert.equal(transcribingLabel(), 'Transcrevendo...');
  });

  it('gravador de teste devolve texto para o transcritor falso', async () => {
    const session = startTestRecording();
    assert.equal(typeof session.elapsedSeconds(), 'number');
    const clip = await session.stop();
    assert.equal(clip.fileName, 'teste-voz.txt');
    assert.equal(clip.mimeType, 'text/plain');
    const text = await clip.blob.text();
    assert.match(text, /hepatite/);
  });
});
