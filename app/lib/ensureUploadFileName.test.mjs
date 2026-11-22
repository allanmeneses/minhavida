import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { ensureUploadFileName } from './ensureUploadFileName.ts';

describe('ensureUploadFileName', () => {
  it('keeps a name that already has an extension', () => {
    assert.equal(ensureUploadFileName('foto.png', 'image/jpeg', 'foto.jpg'), 'foto.png');
  });

  it('adds jpeg extension when the picker omits it after crop', () => {
    assert.equal(ensureUploadFileName(null, 'image/jpeg', 'foto.jpg'), 'foto.jpg');
    assert.equal(ensureUploadFileName('cropped', 'image/jpeg', 'foto.jpg'), 'cropped.jpg');
  });

  it('adds png extension from mime type', () => {
    assert.equal(ensureUploadFileName('avatar', 'image/png', 'foto.jpg'), 'avatar.png');
  });

  it('keeps document names with extension', () => {
    assert.equal(ensureUploadFileName('contrato.txt', 'text/plain', 'arquivo'), 'contrato.txt');
  });
});
