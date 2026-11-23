import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'auth-storage.ts'), 'utf8');
const apiSource = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'api.ts'), 'utf8');
const authSource = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '../context/AuthContext.tsx'), 'utf8');

describe('cookie httpOnly na web', () => {
  it('nao guarda o refresh no storage da web e manda X-Client', () => {
    assert.match(source, /if \(Platform\.OS === 'web'\) \{\s*await remove\(REFRESH_KEY\)/s);
    assert.match(apiSource, /headerInit\['X-Client'\] = 'web'/);
    assert.match(apiSource, /credentials: Platform\.OS === 'web' \? 'include'/);
    assert.match(authSource, /if \(!stored && Platform\.OS !== 'web'\)/);
    assert.match(authSource, /if \(refresh \|\| Platform\.OS === 'web'\)/);
    assert.match(authSource, /refreshToken: refresh \?\? ''/);
  });
});
