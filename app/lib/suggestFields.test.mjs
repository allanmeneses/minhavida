import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { addField, enabledFields, fieldKindLabel, fieldsToDraft, renameField, toggleField } from './suggestFields.ts';

describe('chips de campos sugeridos', () => {
  const fields = [
    { id: 'title', name: 'Título', kind: 'text', required: true, order: 0 },
    { id: 'distancia', name: 'Distância', kind: 'number', required: false, order: 1 },
  ];

  it('liga, desliga, renomeia e adiciona sem apagar o rascunho', () => {
    const draft = fieldsToDraft(fields);
    draft[0].name = 'Título já digitado';
    const toggled = toggleField(draft, 'distancia');
    assert.equal(toggled.find((item) => item.id === 'distancia')?.enabled, false);
    assert.equal(toggled.find((item) => item.id === 'title')?.name, 'Título já digitado');
    const renamed = renameField(toggled, 'title', 'Nome da corrida');
    const added = addField(renamed, 'Ritmo');
    const enabled = enabledFields(added);
    assert.deepEqual(
      enabled.map((item) => item.id),
      ['title', 'ritmo'],
    );
    assert.equal(enabled[0].name, 'Nome da corrida');
    const otherFn = (current) => toggleField(current, 'title');
    const again = (current) => toggleField(current, 'title');
    assert.notEqual(otherFn, again);
    assert.equal(again(added).find((item) => item.id === 'title')?.name, 'Nome da corrida');
  });

  it('rotula o tipo do campo em pt-BR sem Nome de', () => {
    assert.equal(fieldKindLabel({ id: 'title', kind: 'text' }), 'texto');
    assert.equal(fieldKindLabel({ id: 'distancia', kind: 'number' }), 'número km');
    assert.equal(fieldKindLabel({ id: 'duracao', kind: 'number' }), 'número min');
    assert.equal(fieldKindLabel({ id: 'when', kind: 'date' }), 'data');
    assert.equal(fieldKindLabel({ id: 'where', kind: 'place' }), 'lugar');
    assert.equal(fieldKindLabel({ id: 'done', kind: 'bool' }), 'sim/não');
  });
});
