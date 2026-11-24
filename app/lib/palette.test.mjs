import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { darkColors, highContrastDark, highContrastLight, lightColors, paletteFor } from '../constants/theme.ts';

describe('contraste alto', () => {
  it('troca a paleta sem misturar com o tema normal', () => {
    assert.equal(paletteFor('light', false), lightColors);
    assert.equal(paletteFor('dark', false), darkColors);
    assert.equal(paletteFor('light', true), highContrastLight);
    assert.equal(paletteFor('dark', true), highContrastDark);
    assert.equal(highContrastLight.ink, '#000000');
    assert.equal(highContrastLight.bg, '#FFFFFF');
    assert.equal(highContrastLight.line, '#000000');
    assert.equal(highContrastDark.ink, '#FFFFFF');
    assert.equal(highContrastDark.bg, '#000000');
    assert.equal(highContrastDark.line, '#FFFFFF');
    assert.notEqual(highContrastLight.ink, lightColors.ink);
    assert.notEqual(highContrastDark.ink, darkColors.ink);
    assert.ok(lightColors.placeholder);
    assert.notEqual(lightColors.placeholder, lightColors.ink);
    assert.notEqual(lightColors.placeholder, lightColors.muted);
    assert.ok(darkColors.placeholder);
    assert.notEqual(darkColors.placeholder, darkColors.ink);
  });
});
