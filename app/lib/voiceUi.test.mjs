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

  it('timer avança com relógio falso e o rótulo aparece uma vez', () => {
    let now = 1_000_000;
    const session = startTestRecording(() => now);
    assert.equal(session.elapsedSeconds(), 0);
    assert.equal(recordingLabel(session.elapsedSeconds()), 'Gravando 00:00');
    now += 2500;
    assert.equal(session.elapsedSeconds(), 2);
    assert.equal(recordingLabel(session.elapsedSeconds()), 'Gravando 00:02');
    now += 60_000;
    assert.equal(recordingLabel(session.elapsedSeconds()), 'Gravando 01:02');
  });
});
