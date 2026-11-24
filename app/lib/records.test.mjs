import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  acceptPayload,
  extraFields,
  kindFromSlug,
  quickFields,
  recentTypeChips,
  rejectedNotice,
  suggestionDraft,
  tagsFrom,
  transcriptionUnavailable,
  voiceNeedsNetwork,
} from './records.ts';

const fields = [
  { id: 'title', name: 'Título', kind: 'text', required: true, order: 0 },
  { id: 'when', name: 'Quando', kind: 'date', required: true, order: 1 },
  { id: 'where', name: 'Onde', kind: 'place', required: false, order: 2 },
  { id: 'lote', name: 'Lote', kind: 'text', required: false, order: 3 },
  { id: 'photo', name: 'Foto', kind: 'photo', required: false, order: 4 },
];

describe('captura flexível', () => {
  it('limita a captura rápida a três campos sem foto', () => {
    assert.equal(quickFields(fields).length, 3);
    assert.deepEqual(
      quickFields(fields).map((item) => item.id),
      ['title', 'when', 'where'],
    );
    assert.deepEqual(
      extraFields(fields).map((item) => item.id),
      ['lote', 'photo'],
    );
  });

  it('aceite manda os campos editados; recusa não inventa gravação', () => {
    const suggestion = {
      typeId: null,
      isNewType: true,
      type: {
        name: 'Vacinas',
        description: 'Exemplo',
        slug: 'vacinas',
        galleryTypeId: 'g1',
        existingTypeId: null,
        fields,
      },
      fields: { title: 'Hepatite B', when: '2024-03-10', where: 'UBS Centro' },
      summary: 'Sugerimos o tipo Vacinas.',
      transcript: null,
    };
    const draft = suggestionDraft(suggestion);
    draft.where = 'UBS Centro editada';
    const accepted = acceptPayload(suggestion, draft, null);
    assert.equal(accepted.createTypeIfMissing, true);
    assert.equal(accepted.newType.name, 'Vacinas');
    assert.equal(accepted.fields.where, 'UBS Centro editada');
    const typed = acceptPayload(suggestion, draft, 'tipo-1');
    assert.equal(typed.typeId, 'tipo-1');
    assert.equal(typed.createTypeIfMissing, false);
    assert.equal(rejectedNotice(), 'Nada foi guardado. Você recusou a sugestão.');
  });

  it('chips recentes ordenam por uso e o slug mapeia o exemplo antigo', () => {
    const chips = recentTypeChips(
      [
        { id: '2', name: 'Diário', lastUsedAt: '2026-01-02T00:00:00Z', recordCount: 1 },
        { id: '1', name: 'Vacinas', lastUsedAt: '2026-01-03T00:00:00Z', recordCount: 2 },
        { id: '3', name: 'Pet', lastUsedAt: null, recordCount: 0 },
      ],
      2,
    );
    assert.deepEqual(
      chips.map((item) => item.name),
      ['Vacinas', 'Diário'],
    );
    assert.equal(kindFromSlug('vacinas'), 'vaccine');
    assert.equal(kindFromSlug('diario'), 'diary');
    assert.deepEqual(tagsFrom({ tags: 'corrida, manhã' }), ['corrida', 'manhã']);
    assert.match(voiceNeedsNetwork(), /rede/);
    assert.match(transcriptionUnavailable(), /disponível/);
  });

  it('re-render com nova identidade de função não apaga o rascunho', () => {
    const suggestion = {
      typeId: 't1',
      isNewType: false,
      type: {
        name: 'Vacinas',
        description: '',
        slug: 'vacinas',
        galleryTypeId: null,
        existingTypeId: 't1',
        fields,
      },
      fields: { title: '' },
      summary: 'Sugerimos',
      transcript: null,
    };
    const draft = suggestionDraft(suggestion);
    draft.title = 'texto já digitado';
    const acceptA = (current) => acceptPayload(suggestion, current, 't1');
    const acceptB = (current) => acceptPayload(suggestion, current, 't1');
    assert.notEqual(acceptA, acceptB);
    assert.equal(acceptB(draft).fields.title, 'texto já digitado');
  });
});
