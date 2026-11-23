import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { isLast, nextIndex, TOUR_SCREENS } from './tour.ts';

describe('tour', () => {
  it('só avanca e cobre o app em telas estaticas', () => {
    assert.ok(TOUR_SCREENS.length >= 6);
    assert.equal(nextIndex(0), 1);
    assert.equal(nextIndex(TOUR_SCREENS.length - 1), TOUR_SCREENS.length - 1);
    assert.equal(isLast(TOUR_SCREENS.length - 1), true);
    assert.equal(isLast(0), false);
    const blob = TOUR_SCREENS.map((item) => `${item.title} ${item.body}`).join(' ');
    assert.match(blob, /perfil solo/);
    assert.match(blob, /Painel do dia/);
    assert.match(blob, /Círculos/);
    assert.match(blob, /Offline/);
    assert.match(blob, /backup/);
    assert.match(blob, /calendário/);
    assert.match(blob, /recordações/);
    assert.match(blob, /nome, foto/);
    assert.match(blob, /temporário/);
  });
});
