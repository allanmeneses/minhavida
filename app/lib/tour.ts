export type TourScreen = {
  title: string;
  body: string;
};

export const TOUR_SCREENS: TourScreen[] = [
  {
    title: 'Seu perfil solo',
    body: 'O Circulos comeca pela sua vida, sem exigir um circulo. Vacinas, diario, documentos e listas ficam no perfil, que nasce privado.',
  },
  {
    title: 'Painel do dia',
    body: 'A primeira tela mostra o que vence, o que atrasou, o que vem e quantos circulos estao ativos. Atalhos de um toque registram o que aconteceu.',
  },
  {
    title: 'Circulos',
    body: 'Familia, amizade, cuidado ou evento. Voce cria o seu. Para entrar no de outra pessoa, peca ou aceite um convite. Quem ainda nao entrou num circulo publico ve so nome e descricao.',
  },
  {
    title: 'Lembretes e atalhos',
    body: 'Lembretes ficam no app, com recorrencia. O atalho dispara direto, sem confirmacao. Modo ferias silencia tudo pelo prazo que voce escolher.',
  },
  {
    title: 'Listas compartilhadas',
    body: 'Compras, desejos ou materiais no perfil ou no circulo. Cada lista tem permissao: so ver, adicionar, marcar feito ou gerenciar.',
  },
  {
    title: 'Offline',
    body: 'Sem rede, o app le o que ja estava guardado. Adicionar item e marcar feito entram na fila e sobem quando voce volta. O icone mostra so se esta online ou offline.',
  },
  {
    title: 'Memorias e calendario',
    body: 'O album do perfil e do circulo guarda recordacoes. Na surpresa, a revelacao mostra mensagens e fotos, e cada um guarda a recordacao no proprio perfil. Lembretes aparecem no calendario, com exportacao .ics.',
  },
  {
    title: 'Conta e backup',
    body: 'A sessao usa um acesso curto e um refresh. Segundo fator e opcional. Voce exporta o que e seu, no perfil ou no circulo, se tiver permissao. O grafico mostra o uso de armazenamento.',
  },
];

export function nextIndex(current: number): number {
  return Math.min(current + 1, TOUR_SCREENS.length - 1);
}

export function isLast(current: number): boolean {
  return current >= TOUR_SCREENS.length - 1;
}
