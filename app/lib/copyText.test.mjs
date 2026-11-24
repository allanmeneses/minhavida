import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { calendarFeedUrl } from './copyText.ts';

describe('assinatura do calendário', () => {
  it('monta endereço completo sem exibir a rota crua sozinha', () => {
    assert.equal(
      calendarFeedUrl('http://127.0.0.1:5288', '/api/calendar/feed/abc'),
      'http://127.0.0.1:5288/api/calendar/feed/abc',
    );
    assert.equal(
      calendarFeedUrl('http://127.0.0.1:5288/', 'https://exemplo.com/feed'),
      'https://exemplo.com/feed',
    );
  });
});
