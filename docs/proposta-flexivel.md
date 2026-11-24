# Circulos como geleia: proposta de um modelo flexivel

Este texto responde ao pedido do dono: o diferencial nao e ter vacina, diario ou outro modulo pronto. O diferencial e registrar e controlar a vida pessoal de quem usa, do jeito dela. Vacina, pet, carro, livro e remédio entram como exemplos, nao como lei do produto.

Fala original: "eu dei apenas exemplos para você, como as vacinas, que podem ser salvas no sistema, e você colocou como fixo e que é um diferencial do sistema. O diferencial do sistema é ter a versatilidade de registrar e ter controle de tudo da vida pessoal, e não de coisas fixas que fazem sentido para uma pessoa, e para outras não. O sistema precisa ser como uma geleia que se molda e toma a forma do interesse da pessoa, de forma simples e intuitiva, e não uma série de formulários caretas e ultrapassados que ninguém se interessa."

## 1. O que hoje esta fixo no codigo

O nucleo ja se chama entrada (`LifeEntry`: titulo, texto, numero, data, local, foto, tags) e ja existe area livre (`LifeAreaKind.Custom`). Mesmo assim, o produto nasce e se apresenta com tipos de vida hardcoded. Quem abre o perfil ve Vacinas, Lembretes, Documentos e Diario. O formulario muda por tipo. A retrospectiva rotula esses fluxos com nomes de fabrica. O tour e a tela de privacidade falam de vacina como peca do produto.

Isso precisa virar flexivel. Círculos, papeis, convite, sucessao, plano e conta continuam sendo regras do app, nao "modulos de interesse".

| Modulo ou formulario fixo | Como esta modelado | Arquivos |
| --- | --- | --- |
| Areas Vacinas, Lembretes, Documentos, Diario (e Livre) | Enum `LifeAreaKind` (Vaccine, Reminder, Document, Diary, Custom). Cada usuario ganha as quatro primeiras ao nascer a conta. Tabela `life_areas`. | `backend/src/Circulos.Api/Domain/LifeAreaKind.cs`, `LifeAreaDefaults.cs`, `LifeArea.cs`; `app/lib/labels.ts` (`AREA_KIND_LABEL`); `app/lib/tour.ts` |
| Entrada de vida (formulario unico, dicas por tipo) | Tabela `life_entries`: Title, Body, NumberValue, OccurredOn, Location, Photo, TagsJson (texto JSON, nao jsonb nativo). | `LifeEntry.cs`; `LifeAreasController.cs`; `app/app/(app)/life/[id].tsx`; `app/lib/entryExamples.ts` (placeholders de Hepatite B, UBS, renovar RG) |
| Paginas de diario | Tabela `diary_pages` so se a area for `Diary`. | `DiaryPage.cs`; `LifeAreasController.cs` (rotas `/pages` e busca) |
| Documentos pessoais | Tabela `personal_documents`, tela propria, alem da area Documentos. | `PersonalDocument.cs`; `DocumentsController.cs`; `app/app/(app)/documents.tsx` |
| Lembretes, aniversario e ideias de presente | Tabela `reminders`. Enum `ReminderKind` (General, Birthday). Campos de aniversario: PersonName, AdvanceDays, GiftIdeasJson, foto. Recorrencia em `RecurrenceJson` (texto). Tabela `gift_idea_shares`. | `Reminder.cs`, `ReminderKind.cs`, `GiftIdeaShare.cs`; `app/app/(app)/reminders.tsx` |
| Atalhos de um toque | Tabela `shortcuts`. Enum `ShortcutActionKind` (MarkDone, Start, Note) com cores fixas. | `Shortcut.cs`; `ReminderKind.cs` (`ShortcutPalette`); `app/app/(app)/home.tsx` |
| Listas compras, desejos, materiais, outra | Enum `SharedListKind`. Tabelas `shared_lists`, `shared_list_items`, `shared_list_grants`. | `SharedList.cs`; `app/lib/listKinds.ts`; `app/app/(app)/lists.tsx` |
| Perfil vivo pessoa, pet, grupo | Enum `LivingProfileKind`. Tabela `living_profiles` (nome, contexto, nascimento, foto). | `LivingProfile.cs`; `app/app/(app)/living.tsx` |
| Círculo duradouro, temporario, diario | Enum `CircleKind` (Lasting, Temporary, Daily). Tabela `circles`. | `CircleVisibility.cs`; `Circle.cs`; `app/lib/circleKinds.ts`; `app/app/(app)/circles.tsx` |
| Modelos so de lista ou lembrete | Enum `RoutineTemplateKind`. Tabela `routine_templates`. Sementes: Compras da semana, Cuidados do dia, Materiais da escola, Ideias de presente, Tomei o remedio, Corrida da semana, Aniversario, Vacina anual. | `RoutineTemplate.cs`; `TemplateCatalog.cs`; `app/app/(app)/templates.tsx` |
| Memorias | Tabela `memories` (titulo, texto, data, foto). | `Memory.cs`; `app/app/(app)/memories.tsx` |
| Onboarding e recap | Exemplos fixos diary, memory, post. Circulos iniciais Eu e Familia. Recap rotula Vacinas, Diario etc. | `OnboardingService.cs`; `RecapService.cs`; `PrivacyController.cs` |
| Perfil da conta e post no círculo | Tabela `profiles` (nome, bio, nascimento, telefone, e-mail, foto, publico). Tabela `circle_posts` (texto e foto). | `Profile.cs`; `app/components/ProfileForm.tsx`; `CirclePost.cs` |

O formulario de entrada e o mesmo bloco de campos. So a dica muda. Diario ganha paginas extras. Aniversario ganha nome da pessoa e lista de presentes. Quem nao toma vacina ainda assim recebe a area Vacinas.

## 2. O que as tendencias de 2025 e 2026 mostram

Quatro ideias se repetem nos produtos que as pessoas realmente usam para "guardar a vida". Nenhuma delas pede um modulo de vacina no codigo.

**Tipos criados pela pessoa, nao pelo fabricante.** No Notion, um banco e um esquema de propriedades (texto, numero, data, selecao, arquivo, formula) que a pessoa monta e depois filtra em vistas ([Database properties](https://www.notion.com/help/database-properties); [API de propriedades](https://developers.notion.com/reference/property-object)). No Tana, um supertag transforma um no em objeto (Tarefa, Pessoa, Viagem) com campos padrao ([Supertags](https://outliner.tana.inc/learn/features/supertags)). No Capacities, tudo e objeto com tipo; a pessoa cria Meeting, Livro, Projeto e define propriedades ([tipos proprios](https://docs.capacities.io/tutorials/custom-content-types); [propriedades](https://docs.capacities.io/reference/properties)). No Anytype, objeto + tipo + propriedades + ligacoes ([Objects](https://doc.anytype.io/anytype/create/objects); [Types](https://doc.anytype.io/anytype/organize/types)).

**Galeria de exemplos, nao de obrigacoes.** O Marketplace do Notion se apresenta com mais de 30 mil modelos ([Templates](https://www.notion.com/templates)). O Anytype tem a ANY Experience Gallery para instalar um pacote de tipos e vistas ([galeria](https://doc.anytype.io/anytype/community/any-experience-gallery)). Vacina, pet e revisao do carro cabem aqui: copiar, editar, ignorar.

**Captura sem formulario: texto, voz ou foto.** Mem 2.0 (1 out 2025): falar solto vira nota organizada; a pessoa captura e o app organiza ([anuncio](https://get.mem.ai/blog/introducing-mem-2-0); [Voice Mode](https://help.mem.ai/features/voice-mode)). Tana transcreve (91 idiomas), preenche campos do supertag se a pessoa citar o nome do campo, e cobra cerca de 50 creditos de IA por 15 minutos ([voice memos](https://outliner.tana.inc/learn/features/mobile-voice-memos)). Reflect usa Whisper na nota do dia e, em 2025, no cursor ([fev 2022](https://reflect.app/blog/february-2022-update); [fev 2025](https://reflect.app/blog/reflect-update-in-line-voice-transcriber)). Day One transcreve ate 10 minutos no iOS, com internet ([audio](https://dayoneapp.com/guides/tips-and-tutorials/audio-recording/)). Journal da Apple aceita foto, audio e transcricao (em ingles, com exclusoes de localidade) e tem widget ([midia](https://support.apple.com/guide/iphone/add-formatting-photos-and-more-iph492ee70a8/ios); [habito](https://support.apple.com/guide/iphone/build-a-journaling-habit-iph70107aec2/ios)). Widget do Notion abre chat, camera ou voz ([widgets](https://www.notion.com/help/mobile-widgets)).

**Metricas que a pessoa inventa, com toque curto.** Bearable deixa criar sintoma, habito, medicacao e esconder o que dispara; a home se customiza ([site](https://bearable.app/); [como configurar](https://bearable.app/support/howto/configure-and-enter-data-into-bearable/)). Exist pede humor em dois toques (escala 1 a 9) e tags ou numeros livres ([mood](https://exist.io/about/mood/)). Daylio: humor + atividades em dois toques, depois grafico e calendario ([Daylio](https://www.daylio.net/)).

**Tela que se adapta, conversa curta, toque em vez de campo.** A Nielsen Norman Group define generative UI como interface gerada na hora para o contexto da pessoa, com o aviso de que interface que muda demais cansa ([genUI, mar 2024](https://www.nngroup.com/articles/generative-ui/)). Em mar 2026, o avanco pratico que elas destacam nao e um dashboard magico: sao botoes, caixas e chips gerados na conversa para a pessoa escolher em vez de redigitar ([botoes e checkboxes](https://www.nngroup.com/articles/genui-buttons-and-checkboxes/)). Chat de site deve ir ao ponto, com sugestao clicavel ([chatbots](https://www.nngroup.com/articles/ai-chatbots-design-guidelines/)).

**O elogio e a reclamacao do Notion (aviso para nos).** Na pagina de pros e contras da G2 (nota 4,6 em 14.021 avaliacoes), o resumo publicado pela propria G2 aponta customizacao flexivel (1.967 mencoes) e, no lado ruim, curva de aprendizado (823), interface confusa (470) e excesso de recursos (449) ([G2](https://www.g2.com/products/notion/reviews?qs=pros-and-cons)). MakeUseOf (1 dez 2024) chama de excesso para quem so quer anotar: curva alta, muita opcao, falta de nota rapida ([artigo](https://www.makeuseof.com/why-note-taking-in-notion-can-be-overkill/)). Um post no Reddit em 16 mar 2025: gente conhecida nao usa porque e complicado demais ([thread](https://www.reddit.com/r/Notion/comments/1jcw3y1/are_we_making_notion_too_complicated_after_6/)).

Leitura para o Circulos: flexibilidade de dado, simplicidade de gesto. Sem virar um construtor de banco de dados.

## 3. Conceito: um Registro, tipos que a pessoa descreve, exemplos na galeria

Uma so peca no meio: **Registro**. E qualquer fato da vida. Texto, numero, data, foto, arquivo, "feito ou nao". A pessoa nao escolhe um modulo de fabrica. Ela guarda.

Em volta do Registro existe **Tipo** (ou colecao). "Vacina", "Revisao do carro", "Livro que li", "Consulta da Luna". O tipo diz quais campos aquele registro costuma ter. A pessoa cria um tipo de tres jeitos, nesta ordem de preferencia:

1. Fala ou escreve o que quer guardar. Exemplo: "quero anotar as vacinas do meu filho, com data, posto e lote". O app sugere um tipo com esses campos. Ela confirma, tira ou acrescenta. So depois aparece o formulario, ja magro.
2. Copia um exemplo da galeria (Vacinas, Pet, Carro, Livros, Remedios). Copia vira tipo dela. Edita. A galeria nao e o produto.
3. Acrescenta um campo na hora, num registro solto, se ainda nao tem tipo. O app pergunta se aquilo vira tipo.

Captura em um passo, na tela Hoje e no widget:

- Escrever uma frase.
- Falar (o app transcreve e sugere campos).
- Fotografar (carteira de vacinacao, nota fiscal, contra-capa). A sugestao e sempre revisavel. Nada grava escondido.

Vistas nascem do tipo, sem a pessoa "montar dashboard": linha do tempo se tem data, calendario se tem data, grafico se tem numero, checklist se tem "feito". Lembrete nasce de qualquer campo de data, com a recorrencia que o app ja tem. Compartilhar continua sendo o Circulos: o mesmo registro aparece em um ou varios círculos, com permissao, sem duplicar. Atalho de um toque continua: "tomei o remedio" grava um registro do tipo Remedios.

A tela Hoje ja tem o germen disso no recap (so mostra fluxo com uso). O proximo passo e a home mostrar os tipos que a pessoa realmente usa, esconder o resto, e oferecer chips do que ela fez nas ultimas semanas.

## 4. Principios de uso

1. **Exemplo nao e modulo.** Vacina some da conta nova. Aparece na galeria e, se a pessoa pedir, na sugestao.
2. **Primeiro o gesto, depois o campo.** Frase, voz, foto ou chip. Formulario longo so se ela abrir "mais detalhes".
3. **Poucos campos visiveis.** Tres no maximo na captura rapida (titulo, data se fizer sentido, um detalhe). O resto e opcional.
4. **A pessoa confirma a sugestao.** IA sugere tipo e campos. Nunca publica sozinha. Da para desligar a sugestao e continuar no manual.
5. **A tela segue o uso.** Tipo sem registro some da home (continua na lista de tipos). Recap, atalhos e chips acompanham o que ela toca.
6. **Círculo e gente, tipo e coisa.** Nao misturar. Familia e um círculo. Vacina e um tipo. A vacina da crianca pode aparecer no círculo Familia.
7. **Nao ser o Notion.** Sem formula, sem relacao entre dez tabelas, sem "monte seu segundo cerebro". Tipo + registro + vista automatica + círculo.

## 5. O que muda no app de hoje (sem perder dado)

Nada se apaga. O que e modulo vira tipo de galeria e, nas contas ja existentes, tipo ja copiado.

- Vacinas, Diario, Documentos da area de vida e Lembretes da area de vida viram tipos com esquema equivalente ao formulario atual (titulo, quando, onde, numero, texto, foto, tags). Entradas `life_entries` viram registros desse tipo.
- Paginas de diario viram registros do tipo Diario (ou paginas livres dentro do mesmo tipo).
- Arquivos de `personal_documents` viram registros do tipo Documento, com o arquivo cifrado que ja existe.
- Lembretes da tabela `reminders` continuam sendo o motor de disparo. Cada um ganha um registro (ou liga o campo de data do tipo). Aniversario vira tipo de galeria, nao um enum.
- Listas continuam listas (item, quantidade, feito). O "tipo" compras/desejos/materiais vira etiqueta de galeria, nao enum obrigatorio.
- Pet e grupo continuam perfil vivo (quem e). Vacina do pet, peso, vermifugo sao tipos de registro ligados aquele perfil.
- Modelos de rotina crescem: alem de lista e lembrete, passam a copiar tipo + campos + vista.
- Tour, onboarding e texto de privacidade param de vender vacina como peca. Passam a vender "guarde o que importa, compartilhe no círculo".
- Enums de círculo (duradouro, temporario, diario), papeis e plano nao entram nessa troca. Sao regra de convivencia, nao de interesse.

## 6. Roadmap curto

**Fase 0, ja visivel sem motor novo.** Tirar Vacinas, Diario e Documentos como areas automaticas de conta nova. Deixar so uma area Livre (ou nenhuma). Mover os quatro para a galeria de modelos. Ajustar tour, recap e privacidade. Contas antigas mantem o que ja tem, com o rotulo "exemplo, voce pode apagar".

**Fase 1, MVP da geleia.** Tipo com campos (texto, numero, data, local, sim/nao, foto, arquivo, lista curta). Registro unico. Captura por frase e por chips. Vistas automaticas: lista e calendario. Lembrete a partir de campo de data, reusando `reminders`. Galeria com uns oito exemplos (vacina, diario, documento, remedio, pet, carro, livro, compra). Compartilhar registro no círculo, como `LifeEntryShare` ja faz. Home que lista tipos com uso recente.

**Fase 2, captura viva.** Voz (gravar no aparelho, transcrever no servidor, pessoa aceita). Foto com leitura de texto na imagem, mesmo fluxo de aceite. Widget de captura rapida (o Journal da Apple e o Notion ja ensinam o gesto). Grafico para campo numero. Atalho gera registro do tipo.

**Fase 3, conversa curta.** A pessoa descreve um tipo novo em uma frase. O app devolve campos em chips para ela marcar. Sem chat infinito. Sem UI que muda o layout inteiro a cada visita (o risco que a NN/g aponta).

## 7. Riscos e como segurar

| Risco | Mitigacao |
| --- | --- |
| Virar Notion (complexo, ninguem usa) | Teto de campos por tipo no gratuito. Sem formula. Sem relacao entre tipos no MVP. Galeria curada, poucos exemplos. |
| Custo de voz, foto e sugestao de campos | Sugestao so no plano pago, ou cota mensal (o Tana ja cobra credito por minuto de voz). Gravacao fica no aparelho ate a pessoa mandar. Desligar no Ajustes. |
| Privacidade (LGPD, dado de saude, foto de documento) | Continuar cifrando arquivo e foto no servidor, chave do app. Nao usar o conteudo para treinar modelo de terceiro sem termo claro. Transcricao com provedor que apaga o audio depois do texto, documentado na politica. |
| Offline | Registro e tipo editam local e sobem depois, como a fila de listas ja faz. Voz e leitura de foto esperam rede, com aviso. Mem 2.0 vende offline primeiro; nos ja temos o icone e a fila. |
| Dado preso no JSON, busca ruim | Colunas extraidas (titulo, data, numero) alem do JSON. Indice GIN no jsonb. Busca unificada que ja existe passa a ler essas colunas. |
| Duas verdades (area antiga e tipo novo) | Uma migracao. Depois so registro. API antiga vira leitura por um tempo, depois some. |

## 8. Desenho tecnico (ASP.NET + Postgres + Expo)

Hoje JSON de tags, recorrencia e modelo de rotina e `string` com tamanho maximo (`TagsJson`, `RecurrenceJson`, `PayloadJson` em `AppDbContext.cs`). O Postgres sabe mais que isso: tipo `jsonb`, busca por chave e indice GIN ([JSON Types](https://www.postgresql.org/docs/current/datatype-json.html)).

Proposta de tabelas novas, sem jogar fora as atuais ate a migracao terminar:

- `record_types`: Id, UserId (nulo se for modelo da galeria), Name, Description, SchemaJson (jsonb: lista de campos com id, nome, tipo, obrigatorio, ordem), OriginTemplateId, CreatedAt.
- `records`: Id, UserId, TypeId, Title, Body, OccurredOn, NumberValue, FieldsJson (jsonb), PhotoStorageKey, CreatedAt, UpdatedAt. Title, OccurredOn e NumberValue sao copias indexaveis dos campos, para lista, calendario e grafico sem abrir o JSON.
- `record_shares`: RecordId, CircleId (mesmo papel de `life_entry_shares`).
- `record_type_views` so se a pessoa desligar uma vista automatica. Por padrao, o app infere: tem data, tem calendario; tem numero, tem grafico.

Campo no SchemaJson, no MVP: `text`, `number`, `date`, `place`, `bool`, `select`, `photo`, `file`. Lembrete: se o campo e `date` e a pessoa liga "me avisar", cria-se (ou atualiza-se) um `Reminder` existente, na mesma transacao. Sem motor paralelo.

EF Core: `Property(x => x.FieldsJson).HasColumnType("jsonb")`. Indice `USING GIN (fields_json jsonb_path_ops)` para pergunta do tipo "lote = X". Filtro por usuario e tipo continua nas colunas relacionais.

Expo (SDK 58, ja no `app/package.json`): captura de foto ja usa `expo-image-picker` ([ImagePicker](https://docs.expo.dev/versions/latest/sdk/imagepicker/)). Voz: gravar com `expo-audio` ([Audio](https://docs.expo.dev/versions/latest/sdk/audio/)) e enviar o arquivo para a API transcrever. A tela de captura e uma barra: Escrever, Falar, Foto, mais chips dos tipos recentes. O formulario gerado e so os campos do SchemaJson, nao um `switch (kind)` como em `entryExamples.ts`. Widget nativo fica para a fase 2.

Migracao: script que, para cada `LifeArea`, cria um `record_type` com os campos que `entryExample` ja mostra, copia cada `LifeEntry` para `records`, e marca a area como migrada. Diario (`diary_pages`) e documentos seguem o mesmo caminho. Rollback: as tabelas antigas ficam so leitura ate o recorte.

## Fontes

Todas as URLs abaixo foram abertas nesta pesquisa. Numeros e falas vêm da pagina citada, nao de memoria.

1. https://www.notion.com/help/database-properties
2. https://developers.notion.com/reference/property-object
3. https://www.notion.com/templates
4. https://www.notion.com/help/mobile-widgets
5. https://outliner.tana.inc/learn/features/supertags
6. https://outliner.tana.inc/learn/features/fields
7. https://outliner.tana.inc/learn/features/mobile-voice-memos
8. https://docs.capacities.io/tutorials/custom-content-types
9. https://docs.capacities.io/reference/properties
10. https://docs.capacities.io/reference/content-types
11. https://doc.anytype.io/anytype/create/objects
12. https://doc.anytype.io/anytype/organize/types
13. https://doc.anytype.io/anytype/organize/properties
14. https://doc.anytype.io/anytype/community/any-experience-gallery
15. https://get.mem.ai/blog/introducing-mem-2-0
16. https://help.mem.ai/features/voice-mode
17. https://reflect.app/blog/february-2022-update
18. https://reflect.app/blog/reflect-update-in-line-voice-transcriber
19. https://dayoneapp.com/guides/tips-and-tutorials/audio-recording/
20. https://support.apple.com/guide/iphone/add-formatting-photos-and-more-iph492ee70a8/ios
21. https://support.apple.com/guide/iphone/get-started-iph0e5ca7dd3/ios
22. https://support.apple.com/guide/iphone/build-a-journaling-habit-iph70107aec2/ios
23. https://bearable.app/
24. https://bearable.app/support/howto/configure-and-enter-data-into-bearable/
25. https://exist.io/about/mood/
26. https://www.daylio.net/
27. https://www.g2.com/products/notion/reviews?qs=pros-and-cons
28. https://www.makeuseof.com/why-note-taking-in-notion-can-be-overkill/
29. https://www.reddit.com/r/Notion/comments/1jcw3y1/are_we_making_notion_too_complicated_after_6/
30. https://www.nngroup.com/articles/generative-ui/
31. https://www.nngroup.com/articles/genui-buttons-and-checkboxes/
32. https://www.nngroup.com/articles/ai-chatbots-design-guidelines/
33. https://www.postgresql.org/docs/current/datatype-json.html
34. https://docs.expo.dev/versions/latest/sdk/imagepicker/
35. https://docs.expo.dev/versions/latest/sdk/audio/
