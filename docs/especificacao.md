# Especificacao do Circulos (decisoes da ligacao)

Este documento consolida as decisoes de produto visiveis na transcricao `docs/ligacao-transcricao.md`. Cada item cita o numero da pergunta de origem. Quando o dono pediu sugestao e respondeu Beleza, Concordo, Ok ou Muito bom, a sugestao entra como decisao aceita. Quando a sugestao ficou sem essas falas, o texto marca que nao houve confirmacao explicita.

A v1 atual do repositorio (README e codigo em `main`) e a base da secao final.

## Confirmacoes posteriores do dono

Fora da transcricao original, o dono fechou estas ambiguidades:

- So o app / So web: o produto deve funcionar como app e como web.
- Somos sem raca: o correto e Sem rastro.
- E definitiva? e So uma?: sem contexto, nao confirmadas. Nenhuma regra de produto parte delas.
- Pergunta 229 primeira formula: cortada. Nao inventar a resposta.

## Visao

O Circulos e um hub da vida pessoal. A unidade central e um perfil vivo (pessoa, e tambem pet ou grupo na fala inicial do entrevistador). Em volta dele existem entradas (numero, texto, foto, lembrete) e circulos (familia, amizade, interesse, evento, cuidado). Uma pessoa pode ter um ou milhares de circulos. A mesma entrada pode aparecer em varios circulos, com permissoes diferentes, sem duplicar o dado. (abertura; perguntas 1, 16, 28)

Regra de ouro de uso: se registrar uma acao exigir login, menu e varios passos, a acao morre. Atalhos de um toque (iniciei a corrida, tomei o remedio, iniciei o estudo) sao o jeito de guardar o que aconteceu. (98, 99, 100)

Comecar pelo perfil solo, nao pelo circulo. (95)

## Perfil

- Existe perfil solo, sem circulo nenhum, so registrando a vida da pessoa. (28)
- O perfil solo nasce privado. Da para mudar para publico depois. (29, 30)
- Conteudo do perfil solo: vacinas (quando e onde, como exemplo), lembretes da vida, informacoes que a pessoa esquece, documentos de acesso rapido, e participacao em outros circulos. (31)
- Campos iniciais: o app sugere sessoes do dia a dia no codigo; a pessoa tambem cria sessoes novas. Mais tarde o app pode aprender com o que as pessoas criam. (96, 97)
- Cada tipo de entrada tem um padrao de preenchimento simples, com exemplo. (98)
- Identidade vem antes de conteudo: ao entrar num circulo, a pessoa precisa ver quem esta envolvido (feed para confirmar a pessoa), nao so um resumo de padroes. (1)
- Metricas do perfil acompanham o uso: se a pessoa so usa uma coisa, o recap e so daquilo; outros fluxos aparecem quando ela passa a usa-los. (36)
- Diario livre estilo Notion (sessoes, ideias, texto) fica no perfil solo, nao no circulo. Sem busca por palavra-chave, sem versionamento, sem anexo PDF. Sincroniza ao abrir, nao em tempo real. Exporta texto corrido. (243 a 250)

## Documentos

- Documentos pessoais ficam no perfil solo para acesso rapido. (31)
- Precisam ficar criptografados no servidor, nao so no aparelho. (31 reformulada)
- A chave e do app, nao escolhida pela pessoa. (57 reformulada)
- Recuperacao: login web em qualquer aparelho, com esqueci a senha. Se a pessoa perde o aparelho, acessa de outro. (32)
- Se o app fechar, a pessoa nao exporta os dados criptografados em formato que ela mesma abra. Isso e escolha de proposito, para dado pessoal nao vazar. (58, 59)
- Backup pessoal do que e da pessoa num circulo existe (exportar). (7)
- Foto de entrada comprime no upload, criptografa e substitui o original no servidor. Sem limite de tamanho no upload. Qualidade no padrao Instagram. (56, 57, 272, 273, 274)
- Entradas aceitam foto, texto e numero. Video nao. Foto pela camera, galeria ou arquivo do computador. (53, 54, 55)
- Limite de armazenamento: 100 MB no gratuito, 1 GB no pago (sugestao aceita com Ok depois de rejeitar 1 GB / 10 GB). (275)
- Ao chegar perto do limite, avisa com 10% de margem para upgrade, em vez de so bloquear. Grafico de uso existe, sem separar por tipo. Sem atalho de limpeza em lote; exclusao de foto uma a uma e possivel, mas o dono pediu para sair do escopo o fluxo tipo Instagram de limpar foto. (277 a 283)

## Seguranca e criptografia

- Documentos e fotos pessoais criptografados no servidor, chave gerenciada pelo app. (31 reformulada, 57, 57 reformulada)
- Recuperacao de senha so por e-mail no inicio. Segundo fator fica fora do escopo inicial. (33, 34)
- Autenticacao JWT. (69 reinicio)
- Sugestao do entrevistador (sem Beleza/Concordo/Ok visivel): access token de 15 minutos, refresh de 7 dias, renovacao silenciosa, cookie httpOnly. (70, 71, 72)
- WebSocket para notificacao em tempo real, sempre com TLS. (73, 74)
- Estatisticas de uso anonimas podem ser guardadas so para o proprio app, nunca vendidas. (43, 44)
- Repositorio privado. (83)
- Pipeline automatico: build, testes unitarios, integracao, checagem de seguranca, Dependabot. GitHub Actions. (79 a 84)
- Testes automatizados desde o inicio; nada sobe sem teste, validacao, revisao independente (pipeline), deploy e teste de novo. (79, 80)

## Circulos

- Dono fixo, que pode mudar. Historico e permissoes ficam com o circulo, nao resetam. Podem existir varios donos/gestores para o circulo continuar. (2, 3)
- Sem dono ativo em caso sensivel (cuidado de idoso, medicacao): trava e espera. Ninguem assume sozinho. Alguem precisa definir o responsavel. (4)
- Circulo nunca trava na exclusao de conta: transfere para administrador ou sucessor nomeado; se nao houver, abre votacao. (327 a 331, 330)
- Vida util: temporario (evento, curso, remedio), duradouro (anos) ou diario (cria, cumpre, some). (16)
- Temporario encerrado: dados arquivados para consulta, com retrospectiva automatica no fim do ciclo. (17, 18)
- Compartilhar retrospectiva: a pessoa conta a historia dela, nao os dados dos outros. (19, 20)
- Arquivamento pode ser beneficio de plano. Retencao no gratuito: indefinida (o entrevistador registrou eterna). (26, 27)
- Publico versus privado: criar o circulo da propria vida e livre. Entrar em circulo de outro pede pedido ou convite. Circulos privados: so administradores controlam permanencia. Circulos publicos: qualquer pessoa pode entrar, mas quem ainda nao entrou ve so nome e descricao, nao as entradas. (8, 9)
- Busca por interesse em comum e, com opt-in, por localizacao. So aparece quem marcou aceitar localizacao. Quem define isso e o dono sozinho. Circulos que nao querem ser encontrados precisam ser respeitados (invisiveis por padrao). (10, 11, 12, 12 reformulada)
- Feed de descoberta existe, so com dados internos do app, com opcao de silenciar sugestoes. (37, 38, 39)
- Entrada apagada deixa rastro de que existiu, com quem apagou e quando. (5, 6)
- Log de atividades visivel para todos do circulo, para sempre, com data/hora no fuso da pessoa. Filtro por pessoa e por tipo foi sugestao sem Beleza/Ok. (267 a 271)
- Circulo surpresa (aniversario): o aniversariante nao ve nome nem descricao. Na revelacao, resumo com mensagens e fotos. Cada participante pode guardar a recordacao no proprio perfil, editavel em silencio. Sair de um circulo e direito de esquecer para si; o circulo segue para os outros. (229 reformulada a 236)
- Pergunta 229 primeira formula (quando a lista de ideias e compartilhada, a outra pessoa pode adicionar ideias ou so [cortado]): sem resposta. Nao inventar se quem recebe a lista pode incluir ideias.
- Templates de rotina: opcao aceita, gratuitos, editaveis por quem copiou, curadoria inicial do dono do produto, ranqueamento e contagem de uso, criador anonimo. Sem nota obrigatoria no comeco; depois o dono aceitou ranqueamento. (47 a 52)

## Papeis e convites

- Niveis citados na abertura: convite por link com so ver, ver e comentar, ou editar junto.
- Cadastro da conta e livre para criar o proprio circulo. Participar de circulo alheio e por pedido ou convite. (8)
- Denuncia interna no circulo. Quem decide: dono/admins e tambem a equipe do app, que pode bloquear o circulo e tira-lo do radar. Aviso ao dono no bloqueio. Recurso existe, com revisao humana, sem prazo definido (fala: Fica aberto 100 prazo). (21, 21 reformulada, 22, 23, 24, 25)
- Exclusao da conta: so apaga o que e da pessoa. Circulos que ela criou transferem para administradores ou sucessor que ela definiu. (325 a 329)
- Sucessao sem sucessor: votacao. 10% dos envolvidos concordando com uma pessoa, ela vira administrador. Candidaturas abertas durante o processo. Fica aberta ate bater 10%. Pode indicar um membro se ninguem se candidatar. Votos visiveis. Resultado no log. Dono que ja excluiu a conta nao cancela. Circulo segue funcionando na votacao. Empate pede nova votacao; se o empate se repetir, aceita novas candidaturas. Historico de votacoes anteriores nao fica visivel. Perdedor pode se candidatar de novo. (331 a 344)
- Pergunta 336 (um membro vota mais de uma vez ou so uma): a fala visivel e So uma?, sem contexto. O dono marcou como nao confirmada. Nao cravar se o voto e unico. Sem essa regra, a votacao de sucessao nao entra no codigo.
- Pergunta 345 (remover admin recem-eleito) nao teve decisao: o dono encerrou com Cansar esse pedido.

## Publicacoes

- Entrada: numero, texto, foto, lembrete com data; tags opcionais na fala inicial do entrevistador.
- Sem video nas entradas. (53)
- Retrospectiva automatica no fim do ciclo, com cara de post para a pessoa querer compartilhar a narrativa dela. (17, 18, 19)
- Painel do dia e a primeira tela: o que vence, o que esta atrasado, o que vem. Mostra quantos circulos ativos (sugestao aceita com Ok). Atualiza sozinho quando algo muda. (251 a 254)
- Botao de mais no painel para criar entrada sem navegar, sugerindo o contexto (ex.: tomei se o lembrete atrasado e medicacao): sugestao sem Ok/Beleza. (255, 256)
- Busca unificada (entradas, circulos, aniversarios, espaco livre) com filtro opcional por tipo; historico de buscas sincronizado entre dispositivos; termos frequentes no topo. (257 a 260)
- Offline: guarda local e sincroniza ao voltar. Conflito: ultima sincronizada vence, aviso informativo (nao lado a lado). Icone online/offline. (261 a 266)
- Edicao substitui de vez, sem versao anterior. (68)
- Exclusao LGPD: pede o dono do dado, confirma por e-mail, mantem rastro anonimo. Pessoa corrige o que quiser porque o dado e dela. (63 a 67)

## Lembretes e recorrencia

- Lembrete e funcao do app, nao de outro calendario. Integracao com calendario do aparelho e so leitura, se existir. (45, 46, 223)
- E-mail de lembrete e opcional por pessoa: recebe por e-mail ou so no app. (13, 14)
- Circulo ciclico ja nasce com recorrencia; o atalho marca que foi feito. (215)
- Recorrencia no modelo Teams: diaria, semanal, mensal, anual, a cada N dias, a cada N semanas com dias, mensal/anual por data ou por posicao, termino por data ou por numero de ocorrencias, inclusive a cada duas semanas. (216)
- Pausa sem perder historico; lembretes param na hora; ao retomar, segue a data original (a narrativa nao quebra). Excecao pontual: nao dispara, sem marca extra no calendario. (217 a 221)
- Aviso de fim de recorrencia (ex.: ultimos tres disparos): sim. Aniversario com antecedencia configuravel, ou so no dia. (222)
- Aniversario de pessoa, pet ou data comemorativa, com foto e lembrete de presente. Lista de ideias montada pela pessoa, compartilhavel no circulo. (224 a 228)
- Modo ferias silencia tudo; lembretes ficam guardados; prazo escolhido pela pessoa; ao voltar, so o que ainda faz sentido. (239 a 242)

## Atalhos

- Atalhos de uma acao, posicionaveis livremente, varios na mesma tela. (98, 102, 103)
- Disparo: icone na tela inicial do celular e tecla no computador. Sem gesto de balancar ou desenhar. Aceito com Concordo. (100)
- Executa direto, sem confirmacao. Feedback visual de que recebeu o estimulo. Sugestao de 1 segundo sem Ok. Cor muda por tipo de acao. So o nome da acao (fala: So comandacao). (101, 104, 105, 106, 107)
- Som e vibracao opcionais: bip padrao, vibracao curta. (109, 200, 201)
- Desativar some da tela principal ate reativar, mas continua acessivel. Excluir de vez e possivel; entradas ja criadas ficam no historico; atalho excluido pode ser alterado/recuperado. (202 a 207)
- Limite sugerido (sem Ok): 5 no gratuito, ilimitado no pago. Teto de ativos sugerido (sem Ok): 20. (108, 208)
- Compartilhar atalho no circulo: a outra pessoa ve que veio de voce; quem toca registra no mesmo lugar; so o dono edita; aviso de edicao mostra o que mudou. (209 a 214)

## Notificacoes

- Em tempo real, dentro dos limites da aplicacao. A pessoa controla o jeito. (40, 40 reformulada)
- WebSocket com TLS. (73, 74)
- E-mail opcional por preferencia. (14)
- Interface do app e muda (sem som de toque ao salvar). (287)

## Privacidade

- Termo de uso e politica de privacidade no dia um. (60)
- Consentimento geral no comeco; depois o dono mudou: precisa estar adequado a LGPD (revogacao/exclusao). (61, 62, 63)
- Exclusao: dono do dado, confirmacao por e-mail, rastro anonimo permanece. (64, 65, 66)
- Sem painel de transparencia do que foi coletado. (69)
- Circulos invisiveis por padrao se nao quiserem ser encontrados. (12)
- Circulo surpresa com zero vazamento para o aniversariante. (229 reformulada)
- Sair de um circulo: sem rastro para os outros. Confirmado pelo dono: Sem rastro. (238)
- Pergunta 237 (exclusao do circulo na vida da pessoa e reversivel ou definitiva): a fala visivel e E definitiva?, sem contexto. O dono marcou como nao confirmada. Nao cravar reversibilidade.
- Estatisticas anonimas so internamente. (43, 44)
- Excluir conta pede senha, apaga o que e da pessoa, manda e-mail de confirmacao do cancelamento de assinatura, sem link de reativacao nesse e-mail. (323 a 326)

## Plano e monetizacao

- Plano pago desde o inicio, com limites no gratuito. (15)
- Aceito com Muito bom: circulos ilimitados no pago; no gratuito algo como tres; cobrar membro extra em circulos grandes; selo de verificacao anual para perfis publicos de empresas ou profissionais. (15)
- Gratuito limitado o quanto a pessoa quiser dentro dos limites. (60 reinicio)
- Pago alem de mais circulos (aceito com Beleza): circulos com mais de cem membros, retrospectivas em PDF ou imagem, prioridade no suporte. (61 reinicio)
- Atalhos extras, armazenamento maior e retrospectivas bloqueiam se a assinatura suspender; circulos e historico ficam. (315)
- Armazenamento: 100 MB gratis, 1 GB pago. (275)
- Pagina de precos comparando planos, valores em reais. (307, 308)
- Mensal: dono nao cravou valor. Entrevistador sugeriu 25 a 40 reais, sem Ok. Anual com desconto: sim. Dois meses gratis no anual foi fala do entrevistador, nao confirmada. (309, 310)
- Pagamento so cartao, recorrente automatico (dono tirou o Pix depois de ter dito os dois). Cartao recusado: suspende na hora. Volta na hora se pagar de novo. Historico visivel so se esta em dia ou nao, sem valor nem data. Sem nota fiscal por e-mail. Botao de cancelar visivel; acesso pago ate o fim do ciclo; cancela direto, com e-mail de confirmacao, sem link de reativar. (311 a 324)
- Sem programa de indicacao. Sem selo feito no Brasil. Sem blog. Redes sociais oficiais depois. Sem onboarding por e-mail. Sem beta fechado. (300 a 306, 302, 303, 304)
- FAQ no menu, atualiza com o app, sem busca interna, suporte so e-mail sem prazo prometido. Sem ajuda contextual por tela. Tour na primeira abertura, pulavel, reaparece se pedir, cobre tudo, telas estaticas com texto, so avanca. (288 a 299)

## Plataformas

- Abertura: o dono tem tabu de app nativo (muitos aparelhos, lojas, versoes). Entrevistador sugeriu web com PWA e atalho na tela inicial, sem loja. O dono perguntou se o proprio site pode oferecer o atalho: sim.
- Pergunta 305: fala do dono So o app; o entrevistador registrou So web. Confirmacao posterior do dono: o produto deve funcionar como app e como web, nas duas plataformas. O cliente Expo cobre iOS, Android e o navegador.
- Dark mode desde o lancamento, automatico com o tema do aparelho, com override nas configuracoes. (41, 284)
- Acessibilidade: leitor de tela e contraste alto podem entrar. (42)
- Uma lingua no lancamento, estrutura pronta para multilingue. (35)
- Transicao de tela instantanea, interface muda. (286, 287)
- Nome e dominio ficam para o fim; nao batizar agora. Lancar quando ficar pronto. (90, 91, 92)

## Tecnologia

- Backend: C Sharp. (62 reinicio)
- Frontend citado pelo dono: Pode fazer em Nuxt. (63 reinicio)
- Deploy: Azure. (64 reinicio)
- Banco: Postgres. (65 reinicio)
- Acesso a dados: so Entity Framework, nao Dapper. (66 e 67 reinicio)
- API: REST, nao GraphQL. (68 reinicio)
- Auth: JWT. (69 reinicio)
- Fila: simples (tabela) ou NATS com JetStream, a criterio de quem implementa. (76)
- Observabilidade: precisa ser gratuito. Entrevistador sugeriu OpenTelemetry + Grafana Cloud, sem Ok. Retencao indefinida por enquanto. (77, 78)
- Pagina de status automatica; provedor o gratuito mais usado (entrevistador registrou UptimeRobot). (87, 88, 89)
- Conventional Commits: fala visivel Conversao; entrevistador registrou Conventional Commits. Semver: dono indiferente. (85, 86)
- Desenvolver: o entrevistador implementa; o dono gerencia o quanto for necessario. (93, 94)

## Outros

- TokTok Aluguel (outro produto) e PWA, limite de uso do bot e cloud agent aparecem na abertura e nao sao decisoes do Circulos.
- Entrada pode estar em varios circulos ao mesmo tempo (fala do entrevistador na abertura, nao contradita).

## Diferencas em relacao a v1 atual

A v1 em `main` (README e codigo conferidos neste repositorio) entrega so conta, perfil e documentos privados. Nao ha circulos, lembretes, atalhos, publicacoes, plano pago nem criptografia de arquivo.

1. **Documentos nao sao criptografados.** A ligacao exige criptografia no servidor com chave do app (31 reformulada, 57, 57 reformulada). O codigo grava o arquivo em disco local (`LocalFileStorage`) em claro, com `StorageKey` no banco. `PersonalDocument` nao tem campo de cifra. Busca por encrypt/criptograf/AES no codigo: zero ocorrencias.
2. **Foto e documento tem limite fixo, sem compressao Instagram.** v1: foto no maximo 5 MB (`FileRules.MaxPhotoBytes`), documento 10 MB. Ligacao: sem limite de tamanho no upload (272), comprime no padrao Instagram e substitui o original (273, 274). Armazenamento por conta 100 MB / 1 GB (275) nao existe.
3. **Perfil e mais estreito.** v1: nome, foto, data de nascimento, telefone, e-mail secundario (`Profile.cs`, tela `profile.tsx`). Ligacao: perfil solo privado com sessoes (vacinas, lembretes, documentos, diario estilo Notion) e troca privado/publico (28 a 31, 96, 243). Nao ha sessoes livres nem diario.
4. **Foto so da galeria.** v1 usa `ImagePicker.launchImageLibraryAsync` (galeria). Ligacao pede camera, galeria e arquivo do computador (54, 55). `userInterfaceStyle` no `app.json` e `light`; ligacao pede dark mode automatico com override (41, 284).
5. **Sem recuperacao de senha e sem refresh token.** v1: cadastro e login e-mail/senha, JWT de 12 horas (`ExpiresHours`), sem refresh, sem esqueci senha, sem 2FA. Ligacao: esqueci senha so e-mail (33), 2FA fora do inicio (34), JWT com refresh sugerido em 15 min + 7 dias (70 a 72, sem Ok).
6. **Sem circulos, convites, papeis, votacao, denuncia ou feed.** README da v1 coloca circulos na v3. Ligacao trata circulo, dono, convite, publico/privado, surpresa, log, sucessao e votacao como produto. Nada disso esta no modelo (`User`, `Profile`, `PersonalDocument`).
7. **Sem lembretes, recorrencia, atalhos ou painel do dia.** README coloca lembretes na v2. Ligacao exige atalhos de um toque, recorrencia tipo Teams, aniversarios, modo ferias e painel inicial (98, 100, 215, 216, 222, 239, 251).
8. **Cliente e Expo (iOS, Android e web), nao Nuxt nem PWA.** Ligacao: dono citou Nuxt (63 reinicio) e, na abertura, web/PWA para fugir de loja. Confirmacao posterior: precisa ser app e web. v1: Expo SDK 58 + Expo Router, um cliente para as tres plataformas, sem manifesto PWA nem service worker no app. Nao ha `.github/workflows`. Ligacao pede GitHub Actions, Dependabot e testes ja no pipeline (81, 82, 84). Testes xUnit da API existem; nao ha Actions.
9. **Sem plano, pagamento, Azure de producao ou WebSocket.** v1 e local (Docker Postgres, JWT de desenvolvimento). Ligacao: pago desde o inicio, cartao recorrente, pagina de precos em reais, deploy Azure, WebSocket+TLS, fila, status page (15, 64 reinicio, 73, 87, 307, 313).
10. **Privacidade da v1 e isolamento por `UserId`, nao cifra.** README: listar/baixar/apagar filtram por dono, 404 se nao for o dono, chave de storage nao vai ao cliente, `Cache-Control: private, no-store`. Isso nao substitui a criptografia no servidor nem o fluxo LGPD (confirmacao por e-mail, rastro anonimo, exclusao de conta com senha) da ligacao (63 a 66, 325, 326).
11. **Nome ja existe.** Ligacao: nem vou batizar agora (91). v1 ja se chama Circulos no README, `app.json` e JWT Issuer.
12. **Cadastro aberto bate em parte.** v1 permite criar conta com e-mail e senha, alinhado a cadastro livre para o circulo da propria vida (8). Nao ha convite, pedido de entrada nem dois modos de circulo.

Itens da ligacao que a v1 ja cobre, em recorte menor: conta com e-mail e senha, perfil com nome e foto, documentos privados so do dono, Postgres, Entity Framework, API REST, JWT, testes automatizados da API, um cliente que roda em celular e web.
