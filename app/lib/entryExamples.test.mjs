import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { entryExample } from './entryExamples.ts';

describe('exemplos de entrada', () => {
  it('cada tipo tem padrão simples com exemplo', () => {
    const vacina = entryExample('vaccine');
    assert.match(vacina.hint, /quando e onde/);
    assert.equal(vacina.titlePlaceholder, 'Hepatite B');
    assert.equal(vacina.showWhen, true);
    assert.equal(vacina.showWhere, true);
    assert.equal(vacina.wherePlaceholder, 'UBS Centro');

    const diario = entryExample('diary');
    assert.match(diario.hint, /parágrafo do dia/);
    assert.equal(diario.titlePlaceholder, 'Corrida de manhã');
    assert.equal(diario.showWhen, false);

    const documento = entryExample('document');
    assert.match(documento.hint, /acesso rápido/);
    const lembrete = entryExample('reminder');
    assert.equal(lembrete.showWhen, true);
    const livre = entryExample('custom');
    assert.match(livre.hint, /Sem vídeo/);
  });
});
