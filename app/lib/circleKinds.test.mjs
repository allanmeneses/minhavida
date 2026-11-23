import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { canArchiveKind, CIRCLE_KIND_LABEL, CIRCLE_KINDS } from './circleKinds.ts';
import { nextOf } from './listKinds.ts';

describe('tipo do círculo', () => {
  it('alterna duradouro, temporário e diário; só temporário e diário arquivam', () => {
    assert.deepEqual([...CIRCLE_KINDS], ['lasting', 'temporary', 'daily']);
    assert.equal(CIRCLE_KIND_LABEL.lasting, 'Duradouro');
    assert.equal(CIRCLE_KIND_LABEL.temporary, 'Temporário');
    assert.equal(CIRCLE_KIND_LABEL.daily, 'Diário');
    assert.equal(nextOf(CIRCLE_KINDS, 'lasting'), 'temporary');
    assert.equal(nextOf(CIRCLE_KINDS, 'temporary'), 'daily');
    assert.equal(nextOf(CIRCLE_KINDS, 'daily'), 'lasting');
    assert.equal(canArchiveKind('temporary'), true);
    assert.equal(canArchiveKind('daily'), true);
    assert.equal(canArchiveKind('lasting'), false);
  });
});
