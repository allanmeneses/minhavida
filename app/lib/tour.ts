export type TourScreen = {
  title: string;
  body: string;
};

export const TOUR_SCREENS: TourScreen[] = [
  {
    title: 'Seu perfil solo',
    body: 'O Círculos começa pela sua vida, sem exigir um círculo. Vacinas, diário, documentos e listas ficam no perfil, que nasce privado. Se virar público, quem visita vê nome, foto e descrição.',
  },
  {
    title: 'Painel do dia',
    body: 'A primeira tela mostra o que vence, o que atrasou, o que vem e quantos círculos estão ativos. Atalhos de um toque registram o que aconteceu.',
  },
  {
    title: 'Círculos',
    body: 'Família, amizade, cuidado ou evento. Você escolhe se é duradouro, temporário ou diário. Temporário e diário arquivam no fim do ciclo, com a retrospectiva da sua história. Para entrar no de outra pessoa, peça ou aceite um convite.',
  },
  {
    title: 'Lembretes e atalhos',
    body: 'Lembretes ficam no app, com recorrência. O atalho dispara direto, sem confirmação. Modo férias silencia tudo pelo prazo que você escolher.',
  },
  {
    title: 'Listas compartilhadas',
    body: 'Compras, desejos ou materiais no perfil ou no círculo. Cada lista tem permissão: só ver, adicionar, marcar feito ou gerenciar.',
  },
  {
    title: 'Offline',
    body: 'Sem rede, o app lê o que já estava guardado. Adicionar item e marcar feito entram na fila e sobem quando você volta. O ícone mostra só se está online ou offline.',
  },
  {
    title: 'Memórias e calendário',
    body: 'O álbum do perfil e do círculo guarda recordações. Na surpresa, a revelação mostra mensagens e fotos, e cada um guarda a recordação no próprio perfil. Lembretes aparecem no calendário, com exportação .ics. O app lê o calendário do aparelho e nunca grava evento lá.',
  },
  {
    title: 'Conta e backup',
    body: 'A sessão usa um acesso curto e um refresh. Segundo fator é opcional. Você exporta o que é seu, no perfil ou no círculo, se tiver permissão. O gráfico mostra o uso de armazenamento.',
  },
];

export function nextIndex(current: number): number {
  return Math.min(current + 1, TOUR_SCREENS.length - 1);
}

export function isLast(current: number): boolean {
  return current >= TOUR_SCREENS.length - 1;
}
