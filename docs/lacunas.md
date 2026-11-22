# Lacunas: ligacao x codigo desta branch

Fonte: `docs/ligacao-transcricao.md` (perguntas e respostas do dono) e a consolidacao em `docs/especificacao.md`. Conferencia no codigo da branch `cursor/pronto-para-uso-e4d5` (`backend/` e `app/`). Cada linha aponta arquivo, teste ou a ausencia. Inferencia de relatorio antigo nao entra.

Legenda:

- **feito e testado:** codigo existe e ha teste nomeado (xUnit, `npm test` ou Playwright).
- **feito sem teste:** codigo existe; nao achei teste que prove o comportamento.
- **parcial:** parte do pedido esta no codigo; o que falta vai escrito.
- **nao feito:** nao achei codigo.
- **sem confirmacao:** o dono nao fechou a regra; nao inventar implementacao.

Numero da pergunta: transcricao. A segunda serie 60-69 aparece como `N (reinicio)`.

## Postgres e biblioteca de imagem (esta rodada)

### Postgres

Decisao: banco Postgres (65 reinicio). Acesso so Entity Framework, nao Dapper (66 e 67 reinicio).

Observado no codigo:

- `Program.cs`: `Database:Provider` padrao `Postgres`; `UseNpgsql`. Sqlite so se `Database__Provider=Sqlite`.
- `ApiFactory`: `UseNpgsql`, banco `circulos_test_` por factory, `Migrate()` no cliente.
- CI (`.github/workflows/ci.yml`): service `postgres:16-alpine` nos jobs `backend` e `app`.
- `docker-compose.yml`: `postgres:16-alpine`.
- Migracoes `Inicial` e `VidaCirculos`: `uuid`, `timestamp with time zone`.
- Grep em `backend/src`: zero ocorrencias de `Dapper`.

Observado nesta continuacao via API do GitHub: job `backend` do run [45038488241](https://github.com/allanmeneses/minhavida/actions/runs/45038488241) (SHA `5c0e6ef`) conclusion `success`. Job `app` (Playwright contra a API com Postgres) conclusion `success`. Nao reexecutei `dotnet test` local nesta continuacao da analise.

### ImageSharp / SkiaSharp

`Circulos.Api.csproj` referencia `SkiaSharp` 3.119.4 e `SkiaSharp.NativeAssets.Linux.NoDependencies` 3.119.4. Grep `ImageSharp` em `*.cs` e `*.csproj`: zero. Compressao: `ImageCompressor.MaxEdge = 1080`, `JpegQuality = 82`. Teste: `Foto_ComprimeSubstituiOriginalEFicaCifrada`.

Avisos do pacote antigo 3.1.12, conferidos nesta rodada em https://www.nuget.org/packages/SixLabors.ImageSharp/3.1.12 (lista os GHSA abaixo). Identificadores lidos em `https://api.github.com/advisories/{ghsa}` no campo `identifiers` (tipo CVE) e no campo `cve_id`:

| Advisory | CVE | Gravidade na API | Resumo da advisory | Encaixa no uso antigo (`Image.Load` de upload)? |
| --- | --- | --- | --- | --- |
| [GHSA-j3p4-wp97-rph4](https://github.com/advisories/GHSA-j3p4-wp97-rph4) | CVE-2026-106113 | high | `HistogramEqualization` em TIFF `Image<HalfVector4>` | Nao. O compressor so redimensiona e grava JPEG. |
| [GHSA-jjfr-hcj7-qf5w](https://github.com/advisories/GHSA-jjfr-hcj7-qf5w) | CVE-2026-106115 | high | encoder TIFF CCITT T6 | Nao. Este app nao regrava TIFF. |
| [GHSA-j9gm-c75j-xc9q](https://github.com/advisories/GHSA-j9gm-c75j-xc9q) | CVE-2026-106110 | high | encoder TIFF CCITT T4 | Nao. |
| [GHSA-v76p-62qx-wwq2](https://github.com/advisories/GHSA-v76p-62qx-wwq2) | CVE-2026-106118 | high | decoder TIFF fax em tile, escrita fora do buffer | Sim, se o decoder abrisse TIFF. `FileRules.ValidatePhoto` olha extensao e content-type, nao magic bytes. |
| [GHSA-wmxv-xphr-5c9g](https://github.com/advisories/GHSA-wmxv-xphr-5c9g) | CVE-2026-106116 | medium | decoder BigTIFF em loop | Sim no mesmo cenario de decode. |
| [GHSA-gwg2-r3hj-4w44](https://github.com/advisories/GHSA-gwg2-r3hj-4w44) | CVE-2026-106114 | medium | ICC CLUT no parse de perfil | Sim. Perfil ICC pode vir em JPEG/PNG. |

Faixas (API GitHub): 3.1.12 cai em `< 3.2.0`. Correcao gratuita citada: 3.2.0. A 4.x comercial nao foi usada.

Depois da troca, nesta continuacao: `dotnet list src/Circulos.Api/Circulos.Api.csproj package --vulnerable --include-transitive` saiu "no vulnerable packages", exit 0.

## Visao e perfil

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Produto app e web | parcial | confirmacao posterior; 305 | Expo Router web + nativo. Playwright: `cadastro abre o painel do dia, o perfil solo e um circulo`. `app.json` tem bloco iOS; projeto iOS nao foi gerado nesta branch. |
| Comecar pelo perfil solo | feito e testado | 95 | Cadastro cria perfil e areas (`AuthController`, `LifeAreaDefaults`). Teste `Cadastro_CriaPerfilPrivadoComAreasSugeridas`. Playwright abre Hoje, depois Perfil. |
| Perfil solo sem circulo | feito e testado | 28 | Areas no cadastro. Circulo e opcional. |
| Perfil nasce privado e pode virar publico | parcial | 29, 30 | `IsPublic = false` no cadastro. Teste `Perfil_PodeVirarPublico` so muda o flag do dono. Nao ha GET de perfil publico para visitante. O que o visitante veria (31) ficou sem resposta na transcricao. |
| Conteudo: vacinas, lembretes, documentos, diario | feito e testado | 31 | `LifeAreaDefaults`: Vacinas, Lembretes, Documentos, Diario. `Pessoa_CriaAreaNovaEVacinaComQuandoEOnde`, `Diario_ExportaTextoCorrido`. Playwright ve Vacinas e Diario. |
| Sessoes sugeridas + pessoa cria sessoes novas | feito e testado | 96, 97 | POST `/api/life-areas`. Teste `Pessoa_CriaAreaNovaEVacinaComQuandoEOnde`. UI: `profile.tsx`. |
| Aprender sessoes com o que as pessoas criam | nao feito | 96, 97 | Sem codigo de aprendizado. |
| Padrao de preenchimento simples com exemplo | parcial | 98 | Vacina tem Quando/Onde (`life/[id].tsx`). Sem exemplo por tipo alem dos placeholders. |
| Ao entrar num circulo, ver quem esta envolvido | parcial | 1 | Detalhe lista membros (`CirclesController.Get`, `circle/[id].tsx` "Quem esta aqui"). Nao ha tela de descoberta com feed de pessoas antes de entrar. |
| Metricas do perfil acompanham o uso | nao feito | 36 | Sem recap por uso. |
| Diario estilo Notion no solo, sem busca, sem versao, sem PDF, sync ao abrir, exporta texto | parcial | 243 a 250 | Area Diario com texto e export `.txt` (`LifeAreasController` export, `Diario_ExportaTextoCorrido`). Sem sessoes tipo Notion, sem sync offline. Export no app so dispara download se `document` existir (web). |
| Identidade (nome, foto) | feito e testado | abertura, 1 | Perfil + foto. Testes de auth/perfil/foto. Camera e galeria no perfil (`profile.tsx`); Playwright nao exercita esses botoes. |
| Tipo pet ou grupo no perfil | nao feito | abertura (fala do entrevistador) | `Profile` e de usuario pessoa. |
| A mesma entrada em varios circulos sem duplicar | nao feito | abertura | `LifeEntry` e do perfil; `CirclePost` e outro objeto. |
| Feed geral tipo rede social | sem confirmacao | 20 | Transcricao: sem resposta direta visivel. |

## Documentos e fotos

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Documentos no perfil solo | feito e testado | 31 | `/api/documents`, `documents.tsx`. `Dono_EnviaListaBaixaEApagaDocumento`. |
| Criptografados no servidor, chave do app | feito e testado | 31 reformulada, 57, 57 reformulada | `EncryptedFileStorage` AES-256-GCM. `ArquivoNoDisco_NaoContemOTextoOriginal`, `ChaveVemDaConfiguracao_NaoDeUmCampoDeBanco`, `ChaveDeCifra_NaoFicaNoBanco`. |
| Recuperacao: login em qualquer aparelho, esqueci senha | feito e testado | 32, 33 | `forgot-password` / `reset-password`. `EsqueciSenha_SempreNoContentENaoVazaSeOEmailExiste`, `TokenDoEmail_RedefineASenhaUmaVez`. Playwright `esqueci a senha avisa que o email saiu`. E-mail vai para `sent_emails`, nao SMTP. |
| 2FA fora do inicio | nao feito (de proposito) | 34 | Sem TOTP/SMS. |
| Sem exportar cifrado para a pessoa abrir | feito sem teste da negativa | 58, 59 | Download autenticado devolve o arquivo em claro para o dono (`DocumentsController`). Nao ha endpoint de dump cifrado. |
| Backup pessoal do que e da pessoa num circulo | parcial | 7 | Export do diario (`/api/life-areas/{id}/export`). Sem export do circulo (posts, membros, log). |
| Foto comprime, cifra, substitui original | feito e testado | 56, 57, 273, 274 | `ImageCompressor` + `EncryptedFileStorage`. `Foto_ComprimeSubstituiOriginalEFicaCifrada`. Qualidade 82 e o valor no codigo. |
| Sem limite de tamanho no upload | nao feito | 272 | `FileRules.MaxIncomingBytes = 80 MB`. |
| Entradas: foto, texto, numero; sem video | parcial | 53, 54, 55 | `LifeEntry` tem titulo, body, `NumberValue`, foto (POST `/entries/{id}/photo`). UI de area nao expoe campo numerico nem botao de foto. Video nao aparece. |
| Foto por camera, galeria ou arquivo | parcial | 54, 55 | Perfil: camera e galeria. Documentos: `DocumentPicker`. Entrada de vida: API de foto existe, UI nao chama. |
| 100 MB gratis / 1 GB pago; aviso a 90% | parcial | 275, 277 a 283 | `PlanLimits`. `Plano_MostraLimitesSemGateway`, `Armazenamento_DoisEnviosSobrepostosRespeitamOTeto`. Home mostra aviso de storage. Sem grafico (279). Sem limpeza em lote (281; dono pediu para sair do escopo). Excluir foto: `DELETE /api/profile/photo` existe; `profile.tsx` nao tem botao de apagar. |

## Seguranca, auth e pipeline

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| JWT | feito e testado | 69 reinicio | `JwtTokenService`, testes de login/me. |
| Access 15 min + refresh 7 dias + cookie httpOnly | nao feito | 70, 71, 72 (sugestao sem Ok/Beleza) | `ExpiresHours: 12` em `appsettings.json`. Token no corpo JSON. Sem refresh. App guarda em `expo-secure-store`. |
| WebSocket com TLS | nao feito | 73, 74 | Grep em `backend/src`: zero `UseWebSockets` / `SignalR`. |
| Estatisticas anonimas so internas, nunca vendidas | nao feito | 43, 44 | Sem coleta. |
| Repositorio privado | feito sem teste | 83 | API GitHub desta rodada: `private: true` em `allanmeneses/minhavida`. |
| GitHub Actions: build, unit, integracao | feito e testado | 79, 80, 81, 84 | `.github/workflows/ci.yml`: `dotnet test`, `npm test`, `tsc --noEmit`, Playwright. Jobs `backend` e `app` verdes no run desta branch. |
| Checagem de seguranca / Dependabot | nao feito | 82 | Sem `dependabot.yml`. Workflow nao roda `dotnet list --vulnerable`. |
| Testes desde o inicio; nada sobe sem teste | parcial | 79, 80 | Ha xUnit (49 `[Fact]`), `npm test` (6 casos em `lib/*.test.mjs`) e Playwright (5). Nao ha gate de deploy. Deploy nao foi feito. |
| Edicao substitui de vez, sem versao | feito sem teste | 68, 67 | PUT da entrada sobrescreve (`LifeAreasController.UpdateEntry`). Sem tabela de versoes. Sem teste do PUT. |
| Tags opcionais nas entradas | nao feito | abertura | |

## Circulos

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Dono fixo que pode mudar; historico fica | parcial | 2, 3 | Um `OwnerUserId`. Troca so na exclusao de conta (`AccountDeletionService`). Leave do dono responde "precisa transferir" (`CirclesController.Leave`) mas nao ha endpoint de transferencia em vida. `SuccessorEmail` existe no create; a UI nao envia. Teste da troca: `Exclusao_TransfereCirculoParaAdminEAnonimizaLog`. |
| Varios gestores | parcial | 3 | Papel `Admin` existe (`CircleRole`). Nao ha varios donos simultaneos. |
| Sem dono ativo em caso sensivel: trava | parcial | 4 | `CircleStatus.LockedNoOwner` na exclusao sem admin/sucessor. Sem teste que force o lock. Sem trava por inatividade. |
| Circulo nao some na exclusao: transfere ou trava; se nao houver sucessor, votacao | parcial | 327 a 331, 330 | Transfere para admin: testado. Lock existe sem teste. Votacao nao existe. |
| Votacao de sucessao (10%, candidaturas, empate, etc.) | nao feito | 331 a 344; 336 sem confirmacao | Sem entidades de voto. README coloca votacao no depois. 336 e 345 nao cravam regra. |
| Vida util temporario / duradouro / diario | parcial | 16 | Enum `CircleKind` Lasting/Temporary/Daily no create. UI nao escolhe kind (cai no padrao `lasting`). Sem arquivar sozinho. |
| Temporario encerrado: arquivo + retrospectiva | nao feito | 17, 18 | Status `Archived` existe no enum. Nenhum endpoint arquiva nem gera retrospectiva. |
| Compartilhar retrospectiva so a narrativa da pessoa | nao feito | 19, 20 | |
| Arquivamento como beneficio de plano; retencao indefinida | nao feito | 26, 27 | Nao ha compactacao nem exclusao por tempo. Retencao de fato e "fica no banco"; nao ha politica escrita no codigo. |
| Cadastro livre; entrar em circulo alheio por pedido ou convite | parcial | 8 | Convite: `Dono_CriaConviteEOutroAceita`. Join publico direto; privado cria `CircleJoinRequest`. Sem teste do pedido. UI de convite no detalhe; pedido e descoberta sem tela. |
| Publico: quem nao entrou ve so nome e descricao | feito e testado | 9 | `Publico_VisitanteVeSoNomeEDescricao`. |
| Privado: so admins controlam permanencia | parcial | 8 | Aceitar pedido exige `Role >= Admin`. Sem UI de moderar pedidos. |
| Busca por interesse e, com opt-in, por localizacao | nao feito | 10, 11, 12, 12 reformulada | GET `/api/circles/discover` lista publicos com `Discoverable`. Sem geo, sem interesses, sem busca textual. UI nao chama discover. |
| Circulos que nao querem ser encontrados: invisivel por padrao | feito sem teste | 12 | `CreateCircleRequest.Discoverable` e `bool` (false se omitido). UI de criacao nao envia o campo. |
| Feed de descoberta interno, da para silenciar | nao feito | 37, 38, 39 | Discover lista circulos; nao e feed e nao ha silenciar. |
| Entrada apagada deixa rastro com quem e quando | parcial | 5, 6 | `EntradaApagada_DeixaRastroNoLog` prova `action=deleted` e `ActorUserId`. `CreatedAt` vai no DTO. A tela `circle/[id].tsx` mostra `action: detail` e nao mostra data/hora. |
| Log visivel para o circulo, para sempre, fuso da pessoa | parcial | 267 a 271 | GET log no detalhe, visivel a membro. Sem retencao. Sem filtro (270 foi sugestao sem Ok). Sem fuso gravado; UI nao formata `CreatedAt`. |
| Circulo surpresa: aniversariante nao ve; revelacao; recordacao no perfil; sair sem rastro | parcial | 229 reformulada a 236, 238 | `Surpresa_AniversarianteNaoVeOCirculo`, `Sair_NaoDeixaRastroDeParticipacao`. POST `/reveal` existe; sem teste de revelacao e sem botao na UI. Sem resumo com fotos, sem guardar recordacao no perfil, sem edicao em silencio. |
| Lista de ideias compartilhada: a outra pessoa pode incluir? | sem confirmacao | 229 primeira formula (cortada) | Lembrete tem `giftIdeas` so no dono. Nao ha compartilhar no circulo (228). |
| Templates de rotina | nao feito | 47 a 52 | |
| Denuncia interna, bloqueio pela equipe, recurso humano | nao feito | 21 a 25 | |
| Papeis so ver / comentar / editar | parcial | abertura | Enum Viewer/Commenter/Editor/Admin/Owner. Convite aceita `role`. Posts exigem Editor. Commenter nao tem endpoint de comentario. |

## Publicacoes, painel, busca, offline

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Painel do dia primeiro: vence, atrasado, em seguida, quantos circulos | parcial | 251 a 254 | `GET /api/home`, `home.tsx`. Playwright espera "O que pede atencao hoje". `ModoFerias_EscondeLembretesDoPainelEGuardaORegistro`. Atualiza ao recarregar ou apos disparar atalho. Sem atualizacao sozinha quando algo muda (254). |
| Botao + no painel sugerindo contexto | nao feito | 255, 256 (sem Ok) | |
| Busca unificada + historico sincronizado | nao feito | 257 a 260 | |
| Offline: guarda local, ultima sync vence, icone online/offline | nao feito | 261 a 266 | Sem fila local. |

## Lembretes e recorrencia

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Lembrete e funcao do app, nao de outro calendario | feito sem teste da negativa | 45, 46, 223 | CRUD em `/api/reminders`. Sem integracao de calendario. |
| Integracao calendario so leitura, se existir | nao feito | 223 | |
| E-mail de lembrete opcional por pessoa | nao feito | 13, 14 | `OutboxEmailSender` so em convite, esqueci senha e exclusao. Sem preferencia de e-mail no lembrete. Sem disparo agendado. |
| Circulo ciclico com atalho que marca feito | nao feito | 215 | Atalho grava no diario (`HomeController.Run`), nao completa lembrete de circulo. |
| Recorrencia tipo Teams | parcial | 216 | `RecurrenceCalculator` cobre daily/weekly/monthly/yearly, intervalo, daysOfWeek, monthly date/position, until, maxOccurrences. Testes: `DiariaComIntervaloDois_SegueAGradeOriginal`, `SemanalACadaDuasSemanas_Avanca14Dias`, `MensalPorData_UsaODiaDoMes`, `MensalPorPosicao_TerceiraTerca`, `TerminoPorNumero_ParaQuandoCompleta`. Sem teste de yearly. UI (`reminders.tsx`) so cicla none/daily/weekly/yearly; weekly manda `interval: 2`; sem N dias, sem dias da semana, sem termino. |
| Pausa sem perder historico; ao retomar segue a data original | feito e testado | 217 a 221 | `Pausa_MantemProximoDisparoDaGrade`. Skip existe na API (`/skip`); sem botao na UI e sem teste do skip. |
| Aviso de fim (ultimos 3) | feito sem teste | 222 | Campo `endingSoon` em `RemindersController.ToResponse`. UI nao mostra. |
| Aniversario com antecedencia, foto, ideias de presente | parcial | 222, 224 a 228 | `Aniversario_ComAntecedenciaEListaDeIdeias`. Playwright cria aniversario. `Reminder.PhotoStorageKey` existe; nao ha endpoint de foto do lembrete. Ideias nao compartilham no circulo. |
| Modo ferias silencia; ao voltar so o que ainda faz sentido | parcial | 239 a 242 | `ModoFerias_EscondeLembretesDoPainelEGuardaORegistro`, `VoltaDasFerias_IgnoraOneshotQueJaNaoFazSentido`. UI: "Silenciar 7 dias" (`settings.tsx`). Sem prazo livre. |

## Atalhos

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Atalhos de uma acao, varios na tela | feito e testado | 98, 102, 103 | Home lista e dispara. `Atalho_ExecutaSemConfirmacaoEApareceNoPainel`. Playwright `atalho de um toque dispara sem confirmacao`. |
| Posicionaveis livremente | nao feito | 102, 103 | `SortOrder` existe; a UI nao reordena. |
| Icone na tela inicial e tecla no computador | nao feito | 100 | Sem widget, sem atalho de teclado, sem PWA install. 99 ficou sem resposta direta; vale a 100. |
| Sem gesto de balancar | feito (por ausencia) | 100 | Nao ha gesto. |
| Executa direto, sem confirmacao; so o nome | feito e testado | 101, 104 a 107 | POST `/run` sem dialog. |
| Feedback 1 segundo | nao feito | 105 (sem Ok) | `flash` do label permanece ate novo load. |
| Cor por tipo de acao | parcial | 106, 107 | UI cria com `#C45C26`. Sem paleta por tipo. API default se omitir cor: `#1F6F66`. |
| Som e vibracao opcionais | nao feito | 109, 200, 201 | |
| Desativar some da principal; excluir mantem historico | parcial | 202 a 207 | `deactivate` / `activate` / `DELETE` na API. Home so lista `IsActive`. Sem UI desses botoes. DELETE remove a linha; nao ha recuperar atalho excluido (207). |
| Limite 5 gratis / 20 ativos | nao feito | 108, 208 (sem Ok) | Sem teto. |
| Compartilhar atalho no circulo | nao feito | 209 a 214 | |

## Notificacoes e interface

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Tempo real, pessoa controla o jeito | nao feito | 40, 40 reformulada | Sem push, sem WebSocket. Painel so no GET. Anuncios vs organico (40 original) sem resposta. |
| Interface muda (sem som ao salvar) | feito sem teste | 287 | Sem som no save. |
| Dark mode automatico com override | feito e testado (web) | 41, 284 | `userInterfaceStyle: automatic`, `ThemeContext`. Playwright `modo escuro troca a paleta na hora`. |
| Acessibilidade leitor e contraste alto | parcial | 42 | `accessibilityLabel` em `Button` e `TextField`. Sem contraste alto. Sem teste de leitor. |
| Uma lingua, estrutura para mais | parcial | 35 | Textos em portugues no codigo. Sem i18n (chaves/arquivos). |
| Transicao instantanea | nao verificado | 286 | Nao medi animacao. Expo Router usa transicao padrao. |
| Nome e dominio no fim | parcial | 90, 91, 92 | O app ja se chama Circulos (`app.json`, JWT Issuer). Sem dominio de producao. |

## Privacidade e exclusao

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Termo e politica no dia um | nao feito | 60 | Sem telas nem URLs. |
| Adequacao LGPD: revogacao/exclusao | parcial | 61, 62, 63 | Exclusao de conta existe. Sem termo, sem painel do titular. |
| Exclusao: dono do dado, confirma e-mail, rastro anonimo | feito e testado | 64, 65, 66, 325, 326 | `Exclusao_PedeSenhaConfirmaPorEmailEApagaDadosPessoais`, `Exclusao_TransfereCirculoParaAdminEAnonimizaLog`. E-mail de encerramento sem `http`. |
| Sem painel de transparencia | feito (por ausencia) | 69 | Nao ha painel. |
| Sair do circulo sem rastro para os outros | feito e testado | 238 | `Sair_NaoDeixaRastroDeParticipacao`. |
| Exclusao do circulo na vida da pessoa reversivel? | sem confirmacao | 237 | Nao cravar. |
| E-mail de exclusao sem reativar | feito e testado | 323 a 326 | Ver teste acima. SMTP real nao existe; outbox local. |

## Plano e monetizacao

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Plano pago desde o inicio, limites no gratis | parcial | 15, 60 reinicio | Limites 3 circulos e 100 MB (`PlanLimits`, `PlanoGratis_TerceiroCirculoPassaEQuartoFalhaMesmoSobreposto`). `User.Plan` existe; checkout 501 (`Plano_MostraLimitesSemGateway`). |
| Circulos ilimitados no pago; membro extra; selo anual | nao feito | 15 | Sem cobranca de membro, sem selo. |
| Pago: >100 membros, retrospectiva PDF/imagem, suporte prioridade | nao feito | 61 reinicio | |
| Se a assinatura cair, atalhos extras/storage/retrospectivas bloqueiam; historico fica | nao feito | 315 | |
| Pagina de precos em reais | nao feito | 307, 308 | Valor mensal nao cravado (309, 310). |
| So cartao recorrente; recusa suspende; historico so em dia ou nao; sem NF; cancelar visivel | nao feito | 311 a 324 | Pix foi tirado pelo dono; nao ha Pix no codigo. |
| Sem indicacao, sem selo feito no Brasil, sem blog, redes depois, sem onboarding e-mail, sem beta fechado | feito (por ausencia) | 300 a 306 | Nao ha esses produtos. |
| FAQ no menu, tour na primeira abertura | nao feito | 288 a 299 | |

## Tecnologia

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Backend C# | feito e testado | 62 reinicio | ASP.NET Core (`global.json` 10.0.404). Testes xUnit. |
| Frontend Nuxt | nao feito (cliente e Expo) | 63 reinicio | Confirmacao posterior: app e web no Expo. `package.json` Expo `~58.0.26`. Sem Nuxt. |
| Deploy Azure | nao feito | 64 reinicio | `docs/deploy.md` descreve o que faltaria. Nenhum recurso Azure criado nesta rodada. |
| Postgres | feito e testado | 65 reinicio | Ver secao do topo. Job `backend` verde no CI. |
| So EF, nao Dapper | feito e testado | 66 e 67 reinicio | EF Core + Npgsql. Grep `Dapper` em `backend/src`: zero. |
| API REST, nao GraphQL | feito e testado | 68 reinicio | Controllers REST. Grep `GraphQL` em `backend/src`: zero. |
| Fila tabela ou NATS | parcial | 75, 76 | `sent_emails` funciona como outbox de e-mail. Sem worker, sem NATS, sem fila de retrospectiva. |
| Observabilidade gratuita (OTel + Grafana, sem Ok) | nao feito | 77, 78 | Grep `OpenTelemetry` em `backend/src`: zero. |
| Pagina de status (UptimeRobot registrado pelo entrevistador) | nao feito | 87, 88, 89 | |
| Conventional Commits | nao feito | 85, 86 | Mensagens curtas nesta branch, sem prefixo `feat:`/`fix:`. Semver: dono indiferente. |
| PWA com atalho na tela inicial | nao feito | abertura | Sem manifesto PWA, sem service worker. |
| TokTok Aluguel / limite do bot | fora de escopo | abertura | Outro produto. |
| Quem desenvolve / tempo por semana | fora de escopo de codigo | 93, 94 | Processo, nao produto. |

## App Android desta rodada

- Prints em `/opt/cursor/artifacts/android-login.png`, `android-hoje.png`, `android-circulo.png`, `android-lembrete.png` (existem neste disco). Nao reexecutei o emulador nesta continuacao da analise.
- `href: null` nas tabs escondidas impedia abrir Lembretes e o detalhe do circulo no nativo; o codigo agora usa `tabBarButton: () => null`. Sem teste automatizado nativo disso.
- `usesCleartextTraffic: true` em `app.json`.
- Job `android` do run [45038488241](https://github.com/allanmeneses/minhavida/actions/runs/45038488241) (SHA `5c0e6ef`): conclusion `success`. Step `Gerar APK` 15:28:44 a 15:57:50 UTC. Step `Publicar APK` `success`. Artefato `circulos-android` (id `14762035932`, 20927286 bytes, `expired: false`). Download na pagina do run. Jobs `backend` e `app` do mesmo run: `success`. Run inteiro: `success`.

## Contagem

Nao some "feito por ausencia" com entrega de produto. Os buracos que ainda impedem o combinado da ligacao estao nas linhas **parcial** e **nao feito**.
