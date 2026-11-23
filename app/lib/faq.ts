export type FaqItem = {
  question: string;
  answer: string;
};

export const FAQ_ITEMS: FaqItem[] = [
  {
    question: 'O que é o Círculos?',
    answer:
      'Um hub da vida pessoal. Você começa pelo perfil solo, registra o dia a dia e, se quiser, compartilha em círculos de família, amizade, cuidado ou evento. Não há feed geral nem anúncios.',
  },
  {
    question: 'Por onde eu começo?',
    answer:
      'Pelo perfil solo, que nasce privado. O painel do dia é a primeira tela: o que vence, o que atrasou e o que vem. Atalhos de um toque guardam o que aconteceu sem menu extra.',
  },
  {
    question: 'O que fica no perfil solo?',
    answer:
      'Vacinas, lembretes, documentos de acesso rápido, diário em texto e listas só suas. Documentos e fotos vão cifrados no servidor, com chave do app.',
  },
  {
    question: 'Como funcionam os círculos?',
    answer:
      'Cadastro é livre para o círculo da sua vida. Entrar no círculo de outra pessoa pede convite ou pedido. Círculo público mostra só nome e descrição para quem ainda não entrou.',
  },
  {
    question: 'Dá para usar sem internet?',
    answer:
      'Sim. O app guarda localmente o perfil, os lembretes, as listas e o painel do dia. Adicionar item e marcar feito entram numa fila e sobem quando a rede volta. Se o servidor recusar por permissão, a ação some da fila e você é avisado.',
  },
  {
    question: 'Como entra na conta?',
    answer:
      'E-mail e senha, com JWT. O acesso dura pouco e o app renova a sessão em silêncio. No sair, o refresh é revogado. Segundo fator por aplicativo autenticador é opcional.',
  },
  {
    question: 'Como faço backup?',
    answer:
      'Quem tem permissão exporta o perfil ou o círculo em JSON com os arquivos. Não entregamos o material cifrado para você abrir fora do app. Isso é escolha de propósito.',
  },
  {
    question: 'Qual o limite de armazenamento?',
    answer:
      '100 MB no plano gratuito e 1 GB no pago. O app avisa quando chega a 90% e mostra um gráfico de uso, sem separar por tipo.',
  },
  {
    question: 'O recap e o perfil público mostram o que?',
    answer:
      'O recap do perfil só lista o que você já usou. No círculo, a retrospectiva é a história sua, não os dados dos outros. O perfil nasce privado; se virar público, quem visita vê nome, foto e descrição. Telefone, e-mail e diário ficam fora.',
  },
  {
    question: 'Onde ficam as memórias e o calendário?',
    answer:
      'Memórias são a linha do tempo de fotos e textos no perfil ou no círculo, com a mesma permissão de entrada. Lembretes aparecem no calendário do app e da web. Dá para baixar um arquivo .ics ou usar a assinatura. O app lê o calendário do aparelho e nunca grava evento lá. Modelos prontos de lista e lembrete estão em português, gratuitos, e quem copia pode editar.',
  },
  {
    question: 'Quanto custa o plano pago?',
    answer:
      'Preços aprovados pelo Allan em 22/11/2026: R$ 29,90 por mês e R$ 299,00 por ano, em reais, configuráveis. O gratuito cobre 3 círculos, 5 atalhos e 100 MB. A cobrança é no Mercado Pago, ambiente de teste por padrão.',
  },
  {
    question: 'Como falo com o suporte?',
    answer:
      'Só por e-mail, sem prazo de resposta prometido. Você envia pela tela de Ajustes; a mensagem entra na caixa local. O contato do responsável também está na política de privacidade. Não há busca nesta FAQ: role até achar o assunto.',
  },
];
