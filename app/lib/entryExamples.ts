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
        titlePlaceholder: 'Hepatite B',
        showWhen: true,
        showWhere: true,
        whenPlaceholder: 'AAAA-MM-DD',
        wherePlaceholder: 'UBS Centro',
        numberPlaceholder: '1',
        bodyPlaceholder: 'Lote ou observacao',
      };
    case 'diary':
      return {
        hint: 'Exemplo: um paragrafo do dia. Busca por palavra-chave. Sem versao.',
        titlePlaceholder: 'Corrida de manha',
        showWhen: false,
        showWhere: false,
        whenPlaceholder: 'AAAA-MM-DD',
        wherePlaceholder: '',
        numberPlaceholder: '5.2',
        bodyPlaceholder: 'Como foi o treino',
      };
    case 'reminder':
      return {
        hint: 'Exemplo: o que voce nao pode esquecer.',
        titlePlaceholder: 'Renovar RG',
        showWhen: true,
        showWhere: false,
        whenPlaceholder: 'AAAA-MM-DD',
        wherePlaceholder: '',
        numberPlaceholder: '1',
        bodyPlaceholder: 'Detalhe da tarefa',
      };
    case 'document':
      return {
        hint: 'Exemplo: nome do que voce guarda para acesso rapido.',
        titlePlaceholder: 'Carteira de vacinacao',
        showWhen: false,
        showWhere: false,
        whenPlaceholder: 'AAAA-MM-DD',
        wherePlaceholder: '',
        numberPlaceholder: '1',
        bodyPlaceholder: 'Onde esta o original',
      };
    default:
      return {
        hint: 'Texto, numero ou foto. Sem video.',
        titlePlaceholder: 'Nova entrada',
        showWhen: false,
        showWhere: false,
        whenPlaceholder: 'AAAA-MM-DD',
        wherePlaceholder: '',
        numberPlaceholder: '200',
        bodyPlaceholder: 'Opcional',
      };
  }
}
