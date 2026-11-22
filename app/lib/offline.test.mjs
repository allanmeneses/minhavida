import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  applySyncResult,
  discard,
  emptyCache,
  enqueue,
  nextPending,
  readCache,
  writeCache,
} from './offline.ts';

describe('offline cache e fila', () => {
  it('guarda leitura de perfil, lembretes, listas, painel, recap e circulos', () => {
    let cache = emptyCache();
    cache = writeCache(cache, '/api/profile', { displayName: 'Ana' });
    cache = writeCache(cache, '/api/home', { activeCircleCount: 2 });
    cache = writeCache(cache, '/api/reminders', [{ title: 'Remedio' }]);
    cache = writeCache(cache, '/api/lists', [{ name: 'Feira' }]);
    cache = writeCache(cache, '/api/lists/abc', { name: 'Feira', items: [] });
    cache = writeCache(cache, '/api/recap/me', { flows: [{ key: 'vaccine', count: 1 }] });
    cache = writeCache(cache, '/api/circles', [{ name: 'Familia' }]);
    assert.equal(readCache(cache, '/api/profile').displayName, 'Ana');
    assert.equal(readCache(cache, '/api/home').activeCircleCount, 2);
    assert.equal(readCache(cache, '/api/reminders')[0].title, 'Remedio');
    assert.equal(readCache(cache, '/api/lists')[0].name, 'Feira');
    assert.equal(readCache(cache, '/api/lists/abc').name, 'Feira');
    assert.equal(readCache(cache, '/api/recap/me').flows[0].key, 'vaccine');
    assert.equal(readCache(cache, '/api/circles')[0].name, 'Familia');
  });

  it('403 descarta a acao e avisa; a fila nao some num rerender da funcao', () => {
    const action = {
      id: 'a1',
      type: 'add-item',
      listId: 'lista',
      payload: { text: 'Arroz' },
      createdAt: 1,
    };
    let queue = enqueue([], action);
    const sameQueue = enqueue(queue.slice(), {
      id: 'a2',
      type: 'complete-item',
      listId: 'lista',
      payload: { itemId: 'item-1' },
      createdAt: 2,
    });
    assert.equal(nextPending(queue)?.id, 'a1');
    const afterRerender = sameQueue;
    assert.equal(afterRerender.length, 2);
    const flushed = applySyncResult(afterRerender, action, 403, 'Esta lista nao permite adicionar itens.');
    assert.equal(flushed.warning, 'Esta lista nao permite adicionar itens.');
    assert.equal(flushed.queue.length, 1);
    assert.equal(flushed.queue[0].id, 'a2');
    assert.deepEqual(discard(flushed.queue, 'a2'), []);
  });

  it('ultima sincronizada vence: a segunda acao permanece depois da primeira', () => {
    const first = {
      id: '1',
      type: 'complete-item',
      listId: 'lista',
      payload: { itemId: 'item-1' },
      createdAt: 1,
    };
    const second = {
      id: '2',
      type: 'complete-item',
      listId: 'lista',
      payload: { itemId: 'item-1' },
      createdAt: 2,
    };
    let queue = enqueue(enqueue([], first), second);
    queue = applySyncResult(queue, first, 200, '').queue;
    assert.equal(nextPending(queue)?.id, '2');
    queue = applySyncResult(queue, second, 200, '').queue;
    assert.equal(queue.length, 0);
  });
});
