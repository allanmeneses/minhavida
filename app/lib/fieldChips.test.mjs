import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

describe('chips de campo na tela', () => {
  it('uma linha com chip, tipo e lápis, sem Nome de Título', () => {
    const source = readFileSync(join(root, '../components/ui/FieldChips.tsx'), 'utf8');
    assert.match(source, /Renomear \$\{field\.name\}/);
    assert.match(source, /fieldKindLabel/);
    assert.doesNotMatch(source, /Nome de \$\{field\.name\}/);
    const types = readFileSync(join(root, '../app/(app)/types.tsx'), 'utf8');
    assert.match(types, /Nome do tipo/);
    assert.doesNotMatch(types, /Sugestão: \{suggestedName\}/);
    const profile = readFileSync(join(root, '../app/(app)/profile.tsx'), 'utf8');
    assert.doesNotMatch(profile, /Seus tipos/);
    assert.doesNotMatch(profile, /Descrever um tipo/);
    const layout = readFileSync(join(root, '../app/(app)/_layout.web.tsx'), 'utf8');
    assert.match(layout, /label: 'Tipos'/);
    assert.match(layout, /href: '\/\(app\)\/types'/);
    assert.match(layout, /navItem: \{[\s\S]*?paddingVertical: 6/);
    assert.match(layout, /mainNarrow: \{[\s\S]*?overflow: 'auto'/);
    const home = readFileSync(join(root, '../app/(app)/home.tsx'), 'utf8');
    assert.match(home, /onNewType=\{\(\) => router\.push\('\/\(app\)\/types' as Href\)\}/);
  });
});
