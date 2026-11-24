import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('campo de data', () => {
  it('usa campo pt-BR e calendário na web e no aparelho, sem AAAA-MM-DD', () => {
    const source = readFileSync(join(root, '../components/ui/DateField.tsx'), 'utf8');
    assert.match(source, /TextInput/);
    assert.match(source, /isoToDisplayDate/);
    assert.match(source, /MonthGrid/);
    assert.match(source, /Abrir calendário/);
    assert.doesNotMatch(source, /type="date"/);
    assert.doesNotMatch(source, /AAAA-MM-DD/);
    assert.match(source, /datePlaceholder/);
  });
});
