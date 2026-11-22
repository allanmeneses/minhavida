export type FaqItem = {
  question: string;
  answer: string;
};

export const FAQ_ITEMS: FaqItem[] = [
  {
    question: 'O que e o Circulos?',
    answer:
      'Um hub da vida pessoal. Voce comeca pelo perfil solo, registra o dia a dia e, se quiser, compartilha em circulos de familia, amizade, cuidado ou evento.',
  },
  {
    question: 'Por onde eu comeco?',
    answer:
      'Pelo perfil solo, que nasce privado. O painel do dia e a primeira tela: o que vence, o que atrasou e o que vem. Atalhos de um toque guardam o que aconteceu sem menu extra.',
  },
  {
    question: 'O que fica no perfil solo?',
    answer:
      'Vacinas, lembretes, documentos de acesso rapido, diario em texto e listas so suas. Documentos e fotos vao cifrados no servidor, com chave do app.',
  },
  {
    question: 'Como funcionam os circulos?',
    answer:
      'Cadastro e livre para o circulo da sua vida. Entrar no circulo de outra pessoa pede convite ou pedido. Circulo publico mostra so nome e descricao para quem ainda nao entrou.',
  },
  {
    question: 'Da para usar sem internet?',
    answer:
      'Sim. O app guarda localmente o perfil, os lembretes, as listas e o painel do dia. Adicionar item e marcar feito entram numa fila e sobem quando a rede volta. Se o servidor recusar por permissao, a acao some da fila e voce e avisado.',
  },
  {
    question: 'Como entra na conta?',
    answer:
      'E-mail e senha, com JWT. O acesso dura pouco e o app renova a sessao em silencio. No sair, o refresh e revogado. Segundo fator por aplicativo autenticador e opcional.',
  },
  {
    question: 'Como faco backup?',
    answer:
      'Quem tem permissao exporta o perfil ou o circulo em JSON com os arquivos. Nao entregamos o material cifrado para voce abrir fora do app. Isso e escolha de proposito.',
  },
  {
    question: 'Qual o limite de armazenamento?',
    answer:
      '100 MB no plano gratuito e 1 GB no pago. O app avisa quando chega a 90% e mostra um grafico de uso, sem separar por tipo.',
  },
  {
    question: 'Onde ficam as memorias e o calendario?',
    answer:
      'Memorias sao a linha do tempo de fotos e textos no perfil ou no circulo, com a mesma permissao de entrada. Lembretes aparecem no calendario do app e da web. Da para baixar um arquivo .ics ou usar a assinatura. Modelos prontos de lista e lembrete estao em portugues, gratuitos, e quem copia pode editar.',
  },
  {
    question: 'Como falo com o suporte?',
    answer:
      'So por e-mail, sem prazo de resposta prometido. O contato do responsavel esta na politica de privacidade. Nao ha busca nesta FAQ: role ate achar o assunto.',
  },
];
