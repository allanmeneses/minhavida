import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

describe('pipeline de seguranca', () => {
  it('Dependabot cobre nuget, npm e github-actions', () => {
    const text = readFileSync(resolve(root, '.github/dependabot.yml'), 'utf8');
    assert.match(text, /package-ecosystem:\s*nuget/);
    assert.match(text, /directory:\s*\/backend/);
    assert.match(text, /package-ecosystem:\s*npm/);
    assert.match(text, /directory:\s*\/app/);
    assert.match(text, /package-ecosystem:\s*github-actions/);
    assert.match(text, /groups:/);
    assert.match(text, /dotnet-minors:/);
    assert.match(text, /app-minors:/);
    assert.match(text, /github-actions:/);
  });

  it('CodeQL analisa csharp e javascript', () => {
    const text = readFileSync(resolve(root, '.github/workflows/codeql.yml'), 'utf8');
    assert.match(text, /github\/codeql-action\/init@v3/);
    assert.match(text, /language:\s*\[csharp, javascript\]/);
    assert.match(text, /github\/codeql-action\/analyze@v3/);
    assert.match(text, /upload:\s*false/);
    assert.match(text, /codeql-\$\{\{ matrix\.language \}\}/);
    assert.match(text, /actions\/checkout@v7/);
    assert.match(text, /actions\/upload-artifact@v7/);
  });

  it('CI usa as actions atuais de checkout, node, java, dotnet e artifact', () => {
    const text = readFileSync(resolve(root, '.github/workflows/ci.yml'), 'utf8');
    assert.match(text, /actions\/checkout@v7/);
    assert.doesNotMatch(text, /actions\/checkout@v4/);
    assert.match(text, /actions\/setup-node@v7/);
    assert.match(text, /actions\/setup-java@v6/);
    assert.match(text, /actions\/setup-dotnet@v6/);
    assert.match(text, /actions\/upload-artifact@v7/);
    assert.match(text, /package --vulnerable/);
    assert.match(text, /ci-ok:/);
    assert.match(text, /needs: \[backend, app\]/);
  });
});
