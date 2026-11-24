import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { BLOB_REVOKE_DELAY_MS, triggerBrowserDownload } from './browserDownload.ts';

const root = dirname(fileURLToPath(import.meta.url));

function installBrowserMocks() {
  const clicks = [];
  const revoked = [];
  const removed = [];
  const anchors = [];
  const fakeAnchor = {
    href: '',
    download: '',
    click() {
      clicks.push({ href: this.href, download: this.download });
    },
    remove() {
      removed.push(this.href);
    },
  };
  const previous = {
    document: globalThis.document,
    URL: globalThis.URL,
  };
  globalThis.document = {
    createElement: () => {
      anchors.push(fakeAnchor);
      return fakeAnchor;
    },
    body: {
      appendChild(node) {
        assert.equal(node, fakeAnchor);
      },
    },
  };
  let blobSeq = 0;
  globalThis.URL = {
    createObjectURL(blob) {
      blobSeq += 1;
      return `blob:teste-${blobSeq}-${blob.size}`;
    },
    revokeObjectURL(url) {
      revoked.push(url);
    },
  };
  return {
    clicks,
    revoked,
    removed,
    fakeAnchor,
    restore() {
      globalThis.document = previous.document;
      globalThis.URL = previous.URL;
    },
  };
}

describe('download no navegador', () => {
  it('clica com o blob vivo e so revoga depois do atraso', () => {
    const mocks = installBrowserMocks();
    const scheduled = [];
    try {
      triggerBrowserDownload(new Blob(['perfil']), 'circulos-perfil.zip', (fn, ms) => {
        scheduled.push({ fn, ms });
      });
      assert.equal(mocks.clicks.length, 1);
      assert.equal(mocks.clicks[0].download, 'circulos-perfil.zip');
      assert.match(mocks.clicks[0].href, /^blob:teste-1-/);
      assert.equal(mocks.revoked.length, 0);
      assert.equal(mocks.removed.length, 0);
      assert.equal(scheduled.length, 1);
      assert.equal(scheduled[0].ms, BLOB_REVOKE_DELAY_MS);
      scheduled[0].fn();
      assert.deepEqual(mocks.revoked, [mocks.clicks[0].href]);
      assert.deepEqual(mocks.removed, [mocks.clicks[0].href]);
    } finally {
      mocks.restore();
    }
  });

  it('dois downloads seguidos clicam antes de qualquer revoke', () => {
    const mocks = installBrowserMocks();
    const scheduled = [];
    try {
      triggerBrowserDownload(new Blob(['um']), 'circulos-perfil.zip', (fn, ms) => scheduled.push({ fn, ms }));
      triggerBrowserDownload(new Blob(['dois']), 'circulos-casa.zip', (fn, ms) => scheduled.push({ fn, ms }));
      assert.equal(mocks.clicks.length, 2);
      assert.equal(mocks.revoked.length, 0);
      assert.equal(mocks.clicks[0].download, 'circulos-perfil.zip');
      assert.equal(mocks.clicks[1].download, 'circulos-casa.zip');
      assert.notEqual(mocks.clicks[0].href, mocks.clicks[1].href);
    } finally {
      mocks.restore();
    }
  });

  it('exportacao web usa o atraso e nao revoga no mesmo turno', () => {
    const api = readFileSync(join(root, 'api.ts'), 'utf8');
    assert.match(api, /triggerBrowserDownload/);
    assert.doesNotMatch(api, /URL\.revokeObjectURL\(url\);\s*return;/);
  });
});
