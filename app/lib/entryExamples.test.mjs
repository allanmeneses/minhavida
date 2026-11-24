import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { entryExample, fieldPlaceholder } from './entryExamples.ts';

describe('exemplos de entrada', () => {
  it('cada tipo tem padrão simples com exemplo', () => {
    const vacina = entryExample('vaccine');
    assert.match(vacina.hint, /quando e onde/);
    assert.equal(vacina.titlePlaceholder, 'ex.: Hepatite B');
    assert.equal(vacina.showWhen, true);
    assert.equal(vacina.showWhere, true);
    assert.equal(vacina.wherePlaceholder, 'ex.: UBS Centro');
    assert.doesNotMatch(vacina.whenPlaceholder, /AAAA-MM-DD/);
    assert.match(vacina.whenPlaceholder, /24\/11\/2026/);

    const diario = entryExample('diary');
    assert.match(diario.hint, /parágrafo do dia/);
    assert.equal(diario.titlePlaceholder, 'ex.: Corrida de manhã');
    assert.equal(diario.showWhen, false);

    const documento = entryExample('document');
    assert.match(documento.hint, /acesso rápido/);
    const lembrete = entryExample('reminder');
    assert.equal(lembrete.showWhen, true);
    const livre = entryExample('custom');
    assert.match(livre.hint, /Sem vídeo/);
    assert.equal(livre.numberPlaceholder, 'ex.: 1');
    assert.doesNotMatch(livre.numberPlaceholder, /11,8/);
    assert.equal(fieldPlaceholder({ id: 'peso', name: 'Peso', kind: 'number' }, 'pet'), 'ex.: 11,8');
    assert.equal(fieldPlaceholder({ id: 'distancia', name: 'Distância', kind: 'number' }, 'corridas'), 'ex.: 5,2');
    assert.equal(fieldPlaceholder({ id: 'duracao', name: 'Duração', kind: 'number' }, 'corridas'), 'ex.: 32');
    assert.notEqual(
      fieldPlaceholder({ id: 'distancia', name: 'Distância', kind: 'number' }, 'corridas'),
      fieldPlaceholder({ id: 'peso', name: 'Peso', kind: 'number' }, 'pet'),
    );
  });
});
