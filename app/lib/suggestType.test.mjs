import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { suggestTypeFromSentence } from './suggestType.ts';

const cases = [
  ['corri 5 km em 30 minutos no parque', 'Corridas', ['distancia', 'duracao', 'where']],
  ['gastei R$ 45 no almoço', 'Gastos', ['valor']],
  ['terminei o livro O Alienista de Machado de Assis', 'Livro', ['autor']],
  ['quero anotar as vacinas do meu filho, com data, posto e lote', 'Vacinas', ['when', 'where', 'lote']],
  ['o Luna foi no veterinário, pesou 12 kg', 'Pet', ['peso', 'where']],
  ['troquei o óleo do carro aos 12 mil km', 'Carro', ['km']],
  ['tomei o remédio de 500 mg', 'Remédio', ['dose']],
  ['viagem para o Rio, hotel no Centro, voo às 10', 'Viagens', ['where']],
  ['assisti Interestelar no cinema', 'Filmes', ['where']],
];

describe('sugeridor local determinístico', () => {
  for (const [sentence, name, ids] of cases) {
    it(`frase "${sentence}" vira ${name} com campos da frase`, () => {
      const suggestion = suggestTypeFromSentence(sentence);
      assert.equal(suggestion.name, name);
      for (const id of ids) {
        assert.ok(
          suggestion.fields.some((field) => field.id === id),
          `faltou campo ${id} em ${suggestion.fields.map((field) => field.id).join(',')}`,
        );
      }
    });
  }

  it('corrida com km não vira carro', () => {
    const suggestion = suggestTypeFromSentence('corri 5 km em 30 minutos no parque');
    assert.equal(suggestion.name, 'Corridas');
    assert.equal(suggestion.gallerySlug, null);
    assert.ok(suggestion.fields.some((field) => field.id === 'distancia'));
    assert.equal(
      suggestion.fields.some((field) => field.id === 'km' && suggestion.name === 'Carro'),
      false,
    );
  });

  it('só mapeia galeria com confiança alta', () => {
    const km = suggestTypeFromSentence('andei 3 km hoje');
    assert.notEqual(km.name, 'Carro');
    assert.equal(km.gallerySlug, null);
    const carro = suggestTypeFromSentence('revisão do carro com troca de óleo');
    assert.equal(carro.name, 'Carro');
    assert.equal(carro.gallerySlug, 'carro');
  });
});
