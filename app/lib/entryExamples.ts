import { examplePlaceholder } from './placeholders.ts';

export type EntryExample = {
  hint: string;
  titlePlaceholder: string;
  showWhen: boolean;
  showWhere: boolean;
  whenPlaceholder: string;
  wherePlaceholder: string;
  numberPlaceholder: string;
  bodyPlaceholder: string;
};

export function entryExample(kind: string | undefined): EntryExample {
  switch (kind) {
    case 'vaccine':
      return {
        hint: 'Exemplo: vacina, quando e onde.',
        titlePlaceholder: examplePlaceholder('Hepatite B'),
        showWhen: true,
        showWhere: true,
        whenPlaceholder: examplePlaceholder('24/11/2026'),
        wherePlaceholder: examplePlaceholder('UBS Centro'),
        numberPlaceholder: examplePlaceholder('1'),
        bodyPlaceholder: examplePlaceholder('Lote ou observação'),
      };
    case 'diary':
      return {
        hint: 'Exemplo: um parágrafo do dia. Busca por palavra-chave. Sem versão.',
        titlePlaceholder: examplePlaceholder('Corrida de manhã'),
        showWhen: false,
        showWhere: false,
        whenPlaceholder: examplePlaceholder('24/11/2026'),
        wherePlaceholder: '',
        numberPlaceholder: examplePlaceholder('5,2'),
        bodyPlaceholder: examplePlaceholder('Como foi o treino'),
      };
    case 'reminder':
      return {
        hint: 'Exemplo: o que você não pode esquecer.',
        titlePlaceholder: examplePlaceholder('Renovar RG'),
        showWhen: true,
        showWhere: false,
        whenPlaceholder: examplePlaceholder('24/11/2026'),
        wherePlaceholder: '',
        numberPlaceholder: examplePlaceholder('1'),
        bodyPlaceholder: examplePlaceholder('Detalhe da tarefa'),
      };
    case 'document':
      return {
        hint: 'Exemplo: nome do que você guarda para acesso rápido.',
        titlePlaceholder: examplePlaceholder('Carteira de vacinação'),
        showWhen: false,
        showWhere: false,
        whenPlaceholder: examplePlaceholder('24/11/2026'),
        wherePlaceholder: '',
        numberPlaceholder: examplePlaceholder('1'),
        bodyPlaceholder: examplePlaceholder('Onde está o original'),
      };
    default:
      return {
        hint: 'Texto, número ou foto. Sem vídeo.',
        titlePlaceholder: examplePlaceholder('Nova entrada'),
        showWhen: false,
        showWhere: false,
        whenPlaceholder: examplePlaceholder('24/11/2026'),
        wherePlaceholder: '',
        numberPlaceholder: examplePlaceholder('1'),
        bodyPlaceholder: examplePlaceholder('Opcional'),
      };
  }
}

export function fieldPlaceholder(
  field: { id: string; name: string; kind: string },
  typeSlug?: string,
): string {
  const blob = `${field.id} ${field.name}`.toLocaleLowerCase('pt-BR');
  if (field.kind === 'number') {
    if (blob.includes('peso')) {
      return examplePlaceholder('11,8');
    }
    if (blob.includes('dist')) {
      return examplePlaceholder('5,2');
    }
    if (blob.includes('dura')) {
      return examplePlaceholder('32');
    }
    return entryExample(kindFromFieldSlug(typeSlug)).numberPlaceholder;
  }
  const example = entryExample(kindFromFieldSlug(typeSlug));
  if (field.id === 'title') {
    return example.titlePlaceholder;
  }
  if (field.kind === 'place') {
    return example.wherePlaceholder;
  }
  return example.bodyPlaceholder;
}

function kindFromFieldSlug(slug: string | undefined): string {
  switch (slug) {
    case 'vacinas':
      return 'vaccine';
    case 'diario':
      return 'diary';
    case 'documento':
      return 'document';
    case 'lembretes':
    case 'remedio':
      return 'reminder';
    default:
      return 'custom';
  }
}
