import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { planStandingText } from './planStatus.ts';

describe('texto do plano', () => {
  it('plano gratuito nunca fala em inadimplência', () => {
    assert.match(planStandingText('free', 'none', false), /gratuito/);
    assert.doesNotMatch(planStandingText('free', 'none', false), /Inadimplente/);
    assert.doesNotMatch(planStandingText('free', 'none', false), /Nenhuma/);
    assert.match(planStandingText('paid', 'active', true), /Em dia/);
    assert.match(planStandingText('paid', 'past_due', false), /Inadimplente/);
  });
});
