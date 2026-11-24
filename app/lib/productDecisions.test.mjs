import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const appRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = resolve(appRoot, '..');

describe('decisões de produto e contribuicao', () => {
  it('não há feed geral nem anúncios no app', () => {
    const files = [
      readFileSync(join(appRoot, 'app/(app)/home.tsx'), 'utf8'),
      readFileSync(join(appRoot, 'lib/api.ts'), 'utf8'),
      readFileSync(join(appRoot, 'lib/faq.ts'), 'utf8'),
    ].join('\n');
    assert.doesNotMatch(files, /\/api\/feed\b/);
    assert.doesNotMatch(files, /AdMob|adsense|anúncio pago/i);
    assert.match(files, /Não há mural geral nem anúncios/);
    assert.doesNotMatch(files, /\bhub\b/i);
    assert.doesNotMatch(files, /Não há feed geral/);
  });

  it('commits continuam curtos em pt-BR', () => {
    const text = readFileSync(join(repoRoot, 'docs/contribuicao.md'), 'utf8');
    assert.match(text, /curtas em pt-BR/);
    assert.match(text, /sem prefixo `feat:` \/ `fix:`/);
    assert.doesNotMatch(text, /Conventional Commits obrigatorio/);
  });

  it('versionamento do app segue semver 1.0.0', () => {
    const pkg = JSON.parse(readFileSync(join(appRoot, 'package.json'), 'utf8'));
    const expo = JSON.parse(readFileSync(join(appRoot, 'app.json'), 'utf8'));
    assert.match(pkg.version, /^\d+\.\d+\.\d+$/);
    assert.equal(pkg.version, '1.0.0');
    assert.equal(expo.expo.version, '1.0.0');
    assert.equal(expo.expo.name, 'Círculos');
    const contrib = readFileSync(join(repoRoot, 'docs/contribuicao.md'), 'utf8');
    assert.match(contrib, /semver/);
  });

  it('transição entre telas permanece corte seco', () => {
    const rootLayout = readFileSync(join(appRoot, 'app/_layout.tsx'), 'utf8');
    const appLayout = readFileSync(join(appRoot, 'app/(app)/_layout.tsx'), 'utf8');
    assert.match(rootLayout, /animation:\s*'none'/);
    assert.match(appLayout, /animation:\s*'none'/);
  });

  it('não há SMS de lembrete no cliente', () => {
    const settings = readFileSync(join(appRoot, 'app/(app)/settings.tsx'), 'utf8');
    assert.match(settings, /Sem SMS/);
    assert.doesNotMatch(settings, /twilio|smsGateway/i);
  });
});
