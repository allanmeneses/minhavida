# Lacunas: ligacao x codigo desta branch

Fonte: `docs/ligacao-transcricao.md` (perguntas e respostas do dono) e a consolidacao em `docs/especificacao.md`. Conferencia no codigo da branch `cursor/pronto-para-uso-e4d5` (`backend/` e `app/`). Cada linha aponta arquivo, teste ou a ausencia. Inferencia de relatorio antigo nao entra.

Legenda:

- **feito e testado:** codigo existe e ha teste nomeado (xUnit, `npm test` ou Playwright).
- **feito sem teste:** codigo existe; nao achei teste que prove o comportamento.
- **parcial:** parte do pedido esta no codigo; o que falta vai escrito.
- **nao feito:** nao achei codigo.
- **sem confirmacao:** o dono nao fechou a regra; nao inventar implementacao.

Numero da pergunta: transcricao. A segunda serie 60-69 aparece como `N (reinicio)`.

## Rodada 7 (recap, perfil publico, dependencias, lacunas pequenas)

Pedido desta rodada, so o que a especificacao/lacunas sustentam. O que nao estava cravado nao foi inventado.

| Item | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Recap de uso por perfil (so fluxos usados) | feito e testado | 36 | `GET /api/recap/me`. Fluxo com count 0 nao entra. Testes `Recap_Perfil_SoMostraFluxosComUso`. `npm test` cache de `/api/recap/me`. Playwright `recap de uso no perfil so mostra fluxo usado` (inclusive leitura offline do cache). |
| Recap / retrospectiva por circulo, so a historia da pessoa | feito e testado | 17, 18, 19, 20 | `GET /api/circles/{id}/recap`. So posts, memorias e itens da propria pessoa. Visitante 404. Testes `Recap_Circulo_SoAHistoriaDaPessoa_NaoVazaPostDoOutro`, `Recap_VisitanteDoCirculoNaoLe`. Playwright `circulo temporario arquiva e compartilha a historia`. |
| Compartilhar retrospectiva (snapshot, nao dados dos outros) | feito e testado | 19, 20 | `POST /api/circles/{id}/recap/share` com `UserRowLock` e snapshot em `recap_shares`. `GET /api/public/recaps/{token}` anonimo. Post posterior nao entra no snapshot. Testes `Recap_CompartilhaHistoriaPropria_VisitanteNaoVeOsOutros`, `Recap_ShareSobreposto_GeraDoisTokens`. UI `/r/{token}`. Playwright no teste de arquivo. PDF/imagem de retrospectiva (61 reinicio) nao feito. |
| Arquivar circulo temporario ou diario | feito e testado | 16, 17, 18 | `POST /api/circles/{id}/archive` com `UserRowLock`. So Temporary/Daily. Admin+. Idempotente, um log. Viewer 403. Duradouro 400. Testes `Arquivo_TemporarioGeraRecap_DuradouroFalha`, `ArquivoSobreposto_FicaArquivadoUmaVez`, `Arquivo_ViewerRecebe403`. Sem relogio automatico: a data de fim do ciclo nao esta cravada. |
| Tipo do circulo na UI (duradouro / temporario / diario) | feito e testado | 16 | Create envia `kind`. Botao `Tipo:`. `npm test` `alterna duradouro, temporario e diario`. Playwright escolhe Temporario no teste de arquivo. |
| Perfil publico compartilhavel, com privacidade | feito e testado | 29, 30 | Nasce privado. Dono liga `isPublic`. `GET /api/public/profiles/{id}` e `/photo` anonimos; 404 se privado. Payload: userId, displayName, hasPhoto. Sem telefone, e-mail, nascimento, diario. Testes `PerfilPublico_VisitanteVeNomeEFoto_PrivadoDa404`, `Perfil_VoltaPrivado_SomeDaVisaoPublica`. `npm test` rascunho do toggle sobrevive ao rerender. Playwright `perfil publico compartilhavel com privacidade`. UI `/p/{id}`. |
| O que o visitante ve no perfil publico (nome vs feed) | nao cravado | 31 | A transcricao nao escolheu entre identidade e feed. Esta rodada entrega so nome e foto. Diario, documentos, telefone e e-mail ficam fora. |
| Contraste alto | feito e testado | 42 | Paleta `paletteFor` + botao em Ajustes. `npm test` `troca a paleta sem misturar com o tema normal`. Playwright `contraste alto troca a paleta na hora`. Leitor de tela: `accessibilityLabel` ja existia; sem teste de leitor. |
| Exemplo por tipo de entrada | feito e testado | 98 | `entryExample` em `life/[id].tsx`. `npm test` `cada tipo tem padrao simples com exemplo`. Playwright `diario mostra exemplo de preenchimento`. Vacina quando/onde permanece. |
| Edicao substitui de vez, sem versao | feito e testado | 68, 67 | PUT da entrada sobrescreve. Teste `Entrada_EdicaoSubstituiDeVez`. Sem UI de edicao da entrada nesta rodada (gesto de pessoa N/A). |
| Dependencias (Actions, app, .NET, majors) | feito e testado | 82 | Actions: checkout@v7, setup-node@v7, setup-java@v6, setup-dotnet@v6, upload-artifact@v7. App: react/react-dom 19.3.1, reanimated 4.8.0, worklets 0.14.0. .NET: Npgsql 10.0.4, QrCodeGenerator 3.2.1, Test.Sdk 18.12.0, coverlet 10.1.1. SkiaSharp 4.157.0 e TypeScript ~7.0.2: build e testes locais passaram sem mudanca de API. `.github/dependabot.yml` agrupa patch/minor (nuget, npm) e github-actions. `npm test` `Dependabot cobre...` e grupos. |
| Billing, SMTP real, Azure, iOS, votacao, WebSocket, cookie httpOnly (72), conflito offline (262), revelacao agendada (230) | nao feito | pedido desta rodada | Fora do escopo. |

CI observado (API GitHub, lido nesta rodada):

- Squash `b6a322b` no `main`: CI run [45064997353](https://github.com/allanmeneses/minhavida/actions/runs/45064997353) conclusion `failure`. Jobs `backend`, `app` e `android` com `steps: []` e `runner_id: 0`, 2 a 3 segundos. Annotacao do check-run 137339058626: "The job was not started because recent account payments have failed or your spending limit needs to be increased." CodeQL run [45064997483](https://github.com/allanmeneses/minhavida/actions/runs/45064997483) conclusion `failure`, mesmos jobs vazios. Nao e falha de codigo.
- HEAD do PR #21 (`2e8afa94f77f`): CI run [45061785889](https://github.com/allanmeneses/minhavida/actions/runs/45061785889) conclusion `success`. Jobs `backend`, `app` e `android` conclusion `success`. CodeQL run [45061785806](https://github.com/allanmeneses/minhavida/actions/runs/45061785806) conclusion `success`.

Testes locais desta rodada (comandos e exit code):

- `dotnet test --nologo` (backend): Passed 117, Failed 0, exit 0
- `npm test` (app): 26 pass, 0 fail, exit 0
- `npx tsc --noEmit` (app): exit 0
- `npx playwright test` (app): 37 passed, exit 0

## O que ainda depende de decisao do Allan

Lista fechada, sem inventar regra:

- 31: visitante do perfil publico ve so identidade (nome/foto, feito aqui) ou tambem um feed? Sem resposta na transcricao.
- 72: cookie httpOnly (sugestao sem Ok/Beleza).
- 103: arrastar atalho para qualquer ponto da tela.
- 108 e 208: teto de atalhos no plano (sugestao sem Ok).
- 207: recuperar atalho depois de excluir ("Pode ser alterado").
- 230: revelacao surpresa agendada por data (a ligacao descreve o momento da revelacao, nao um relogio).
- 237: exclusao do circulo na vida da pessoa e reversivel? (fala "E definitiva?", sem contexto).
- 255 e 256: botao + no painel (sugestao sem Ok).
- 262: conflito offline entre dois aparelhos (ultima sincronizada vence, sem merge).
- 270: filtro do log (sugestao sem Ok).
- 309 e 310: valor mensal em reais.
- 331 a 344 e 336: votacao de sucessao (336 sem confirmacao; sem essa regra a votacao nao entra).
- 10 a 12 no detalhe: busca por localizacao / geo.
- Login proprio para pet/grupo; edicao do perfil vivo por qualquer membro.
- Cor por tipo de atalho: paleta nao cravada (106, 107).
- Data de fim automatica do ciclo temporario/diario (16 a 18): arquivar e acao do admin, nao relogio.
- Arquivamento como beneficio de plano e retrospectiva em PDF/imagem (26, 27, 61 reinicio).
- Feed de descoberta com silenciar (37 a 39).
- Denuncia interna / bloqueio pela equipe (21 a 25).
- Billing/gateway, SMTP real, deploy Azure, iOS nativo, PWA install, WebSocket, SMS 2FA.

## Rodada 6 (memorias, revelacao, modelos, calendario, pet/grupo, observabilidade)

Pedido desta rodada, so o que a especificacao/lacunas sustentam. O que nao estava cravado nao foi inventado.

| Item | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Album / linha do tempo de memorias por perfil e por circulo | feito e testado | abertura (entrada foto/texto); 229 reformulada a 232 (recordacao no perfil) | `GET/POST /api/memories`, foto `POST /api/memories/{id}/photo`. Permissao: solo so o dono; circulo Viewer le, Editor+ cria, Admin apaga. Surpresa esconde do honrado ate revelar. Testes `Perfil_CriaLinhaDoTempoEEdicaoSilenciosaNaoApagaTexto`, `Circulo_EditorCria_ViewerRecebe403_VisitanteNaoVe`, `Surpresa_HomenageadoNaoVeMemoriaAntesDaRevelacao`, `FotoDaMemoria_ComprimeEFicaDisponivelNoAlbum`, `KeepMemory_SobrepostoDoMesmoUsuario_FicaUmaSo`. `npm test` `rerender com nova identidade de onSave nao apaga o texto digitado`. Playwright `album de memorias no perfil e rascunho sobrevive ao rerender`. |
| Revelacao surpresa: resumo com mensagens e fotos; guardar no perfil; edicao silenciosa | feito e testado | 229 reformulada a 236, 230 | `POST /api/circles/{id}/reveal` idempotente com `UserRowLock`. `GET /reveal-summary`. `POST /keep-memory` com indice unico `AuthorUserId+SourceCircleId`. UI `Revelar surpresa` e `Guardar recordacao no meu perfil`. Testes `Revelacao_DevolveResumoComMensagensEFotos_HomenageadoGuardaNoPerfil`, `RevelacaoSobreposta_OsDoisPassamEFicaRevelado`. Playwright `revelacao surpresa guarda recordacao no perfil`. |
| Revelacao agendada por data (conteudo que aparece so no dia) | nao cravado | 230 | A transcricao fala do momento da revelacao (`POST /reveal`), nao de um `RevealAt` automatico. Nao inventar. |
| Templates de listas e lembretes, pt-BR, gratis, editaveis, ranking/uso, criador anonimo | feito e testado | 47 a 52 | Catalogo inicial no codigo (`TemplateCatalog`). GET incrementa ranking por `UseCount`. POST cria modelo sem devolver o criador. Copia vira lista ou lembrete editavel. Testes `Catalogo_TemModelosPtBrAnonimosECopiaEditavel`, `CopiaLembrete_CriaRecorrenciaEditavel`, `CopiaSobreposta_ContaUsoDuasVezes`, `PessoaCriaModeloDeLista_SemNomeDoCriador`. `npm test` `catalogo inicial esta em portugues e e anonimo`. Playwright `modelos prontos copiam lista em portugues`. |
| Calendario de lembretes no app e na web + exportacao .ics | feito e testado | 223; pedido desta rodada | `GET /api/calendar`, telas `calendar.tsx` (app e web). Download autenticado e assinatura anonima `GET /api/calendar/feed/{token}`. Nao cria evento no calendario do aparelho (46). Testes `Mes_MostraOcorrenciasEExportaIcsComAssinatura`, `Skip_NaoMarcaAOcorrenciaPuladaNoCalendario`, `Feed_PrimeiroAcessoSobreposto_GeraUmTokenSo`, `MontaEventoComResumoEEscapaTexto`. Playwright `calendario mostra lembrete e baixa ics`. |
| Perfil pet ou grupo (alem da conta pessoa) | feito e testado | abertura (fala do entrevistador); 224 | `LivingProfile` kind person/pet/group. Dono edita. Membro do circulo ve, nao edita. Viewer nao cria no circulo. Sem login proprio para o pet. Testes `DonoCriaPetEGrupo_MembroVe_NaoEdita`, `Viewer_NaoLigaPerfilNoCirculo`. Playwright `perfil de pet no circulo`. |
| Edicao de perfil vivo por qualquer membro do circulo | nao cravado | abertura | So o dono edita. Nao inventar papel extra. |
| OpenTelemetry traces e metricas, exportador por env, desligado por padrao | feito e testado | 77, 78 (sugestao; esta rodada pede) | `OTEL_ENABLED` e `OTEL_EXPORTER_OTLP_ENDPOINT`. `Observability:Enabled` false. Testes `OpenTelemetry_DesligadoPorPadrao_LigaPorVariavel`, `npm test` `OpenTelemetry liga por variavel e nasce desligado`. Grafana Cloud fica fora: o exportador e OTLP generico. |
| Health/status + pagina simples | feito e testado | 87, 88, 89 | `GET /health`, `/health/ready`, `/api/status`, HTML `/status`. Teste `HealthEStatus_PublicosEBancoOk`. Playwright `pagina de status publica`. UptimeRobot como provedor externo nao foi cadastrado daqui. |
| Billing, SMTP real, Azure, iOS, votacao, WebSocket, cookie httpOnly (72), conflito offline (262) | nao feito | pedido desta rodada | Fora do escopo. |

CI do main no squash `0600691` (run [45059088563](https://github.com/allanmeneses/minhavida/actions/runs/45059088563)): job `backend` conclusion `success` (Testes da API 19:09:16-19:10:30 UTC). Job `app` conclusion `success` (Playwright web 19:10:02-19:11:54 UTC). Job `android` ainda estava em `Gerar APK` (status `in_progress`) na leitura desta rodada. CodeQL run [45059088493](https://github.com/allanmeneses/minhavida/actions/runs/45059088493) conclusion `success`.

## Rodada 5 (JWT, 2FA, offline, FAQ, backup, Dependabot)

Pedido desta rodada, com o que a especificacao/lacunas sustentam. O que nao estava cravado nao foi inventado.

| Item | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| JWT access curto + refresh com rotacao e revogacao no logout | feito e testado | 69 reinicio; 70 e 71 (sugestao; esta rodada pede) | `Jwt:AccessTokenMinutes` 15, `RefreshTokenDays` 7. `SessionService.RotateAsync` com `UserRowLock`. Logout `RevokeAsync`. Reset de senha `RevokeAllAsync`. Testes `Login_DevolveAccessERefresh_AccessExpiraERefreshRenova`, `RefreshSobreposto_SoUmaRotacaoVence`, `Logout_RevogaRefresh_NaoRenovaMais`, `LogoutSobrepostoComRefresh_NaoDeixaDoisRefreshValidos`, `ResetSenha_RevogaRefresh`. Playwright `access expirado renova com refresh; logout revoga`. App: `AuthContext` + `api` 401. |
| Cookie httpOnly | nao feito | 72 (sugestao sem Ok/Beleza) | nao cravado. Cliente Expo usa Bearer no JSON e `circulos.refresh` no storage. |
| 2FA TOTP opcional (QR, recuperacao, login) | feito e testado | 34 (fora do inicio; esta rodada pede) | `Totp` RFC 6238 SHA1 6 digitos, `TotpQr` PNG, 8 codigos de recuperacao com hash. Testes `AtivarComQr_ExigeCodigoNoLogin_RecuperacaoEntraUmaVez`, `EnableSobrepostoComLogin_NaoEntregaSessaoSemSegundoFator`, `Desativar_PedeSenhaECodigo`, `npm test` `bate no vetor RFC 6238`. Playwright `segundo fator com QR exige codigo no login`. |
| Offline: cache de leitura + fila de listas | feito e testado | 261 a 266 | Cache: perfil, painel, lembretes, listas. Fila: adicionar item e marcar feito. 403 descarta e avisa (`applySyncResult`). Icone Online/Offline. Testes `guarda leitura...`, `403 descarta a acao e avisa`. Playwright `offline le o cache, enfileira item e 403 descarta com aviso` e `offline adiciona item na fila e sobe ao voltar`. |
| Conflito ultima sincronizada vence entre aparelhos | nao cravado | 262 | A fila no mesmo aparelho e serial (a ultima da fila sobe por ultimo). Dois aparelhos ao mesmo tempo nao tem merge. Nao inventar. |
| FAQ e tour inicial pt-BR | feito e testado | 288 a 299 | `FAQ_ITEMS` e `TOUR_SCREENS` no app e na web. Sem busca. Tour pulavel, reaparece em Ajustes, so avanca. Testes `cobre perfil...`, `so avanca e cobre o app`. Playwright `tour na primeira abertura e FAQ no menu`. |
| Backup JSON + arquivos, so com permissao | feito e testado | 7, 58, 59 | `GET /api/export/me` e `/api/export/circles/{id}` zip `dados.json` + `files/`. Membro exporta o circulo; visitante 404. Teste `Perfil_ExportaJsonEArquivo`, `Circulo_MembroExporta_VisitanteNao`. Playwright `backup do perfil e grafico de armazenamento`. Dump cifrado para a pessoa abrir continua fora (58, 59). |
| Grafico de armazenamento sem separar tipo | feito e testado | 279, 280, 275 | Barra em Ajustes, `Uso de armazenamento`, texto "Sem separar por tipo". 100 MB / 1 GB ja em `PlanLimits`. Playwright no teste de backup. |
| Dependabot nuget, npm, github-actions | feito e testado | 82 | `.github/dependabot.yml`. `npm test` `Dependabot cobre nuget, npm e github-actions`. |
| CodeQL no GitHub Actions | parcial | 82 | `.github/workflows/codeql.yml` csharp e javascript. Analise rodou no run [45054514009](https://github.com/allanmeneses/minhavida/actions/runs/45054514009): 56 TS + 11 JS, 90 C#. Upload para code scanning falhou com "Code scanning is not enabled for this repository". Nao da para ligar o recurso daqui. O workflow grava o SARIF como artefato (`upload: false`). `npm test` `CodeQL analisa csharp e javascript`. |
| Billing, SMTP real, Azure, iOS, votacao, WebSocket | nao feito | pedido desta rodada | Fora do escopo. |

CI do main no squash `38ae79f` (run [45052962465](https://github.com/allanmeneses/minhavida/actions/runs/45052962465)): job `backend` conclusion `success`; job `app` conclusion `success`. Job `android` ainda estava em `Gerar APK` (status `in_progress`) na leitura desta rodada; nao falhou.

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

`Circulos.Api.csproj` referencia `SkiaSharp` 4.157.0 e `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.157.0. Grep `ImageSharp` em `*.cs` e `*.csproj`: zero. Compressao: `ImageCompressor.MaxEdge = 1080`, `JpegQuality = 82`. Teste: `Foto_ComprimeSubstituiOriginalEFicaCifrada`. A 4.x entrou nesta rodada depois de `dotnet test` local passar sem mudanca de API.

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
| Perfil nasce privado e pode virar publico | feito e testado | 29, 30 | `IsPublic = false` no cadastro. GET publico anonimo e toggle. Ver secao Rodada 7. O recorte do visitante (31) continua nao cravado: so identidade. |
| Conteudo: vacinas, lembretes, documentos, diario | feito e testado | 31 | `LifeAreaDefaults`: Vacinas, Lembretes, Documentos, Diario. `Pessoa_CriaAreaNovaEVacinaComQuandoEOnde`, `Diario_ExportaTextoCorrido`. Playwright ve Vacinas e Diario. |
| Sessoes sugeridas + pessoa cria sessoes novas | feito e testado | 96, 97 | POST `/api/life-areas`. Teste `Pessoa_CriaAreaNovaEVacinaComQuandoEOnde`. UI: `profile.tsx`. |
| Aprender sessoes com o que as pessoas criam | nao feito | 96, 97 | Sem codigo de aprendizado. |
| Padrao de preenchimento simples com exemplo | feito e testado | 98 | `entryExample` por tipo. Ver secao Rodada 7. |
| Ao entrar num circulo, ver quem esta envolvido | parcial | 1 | Detalhe lista membros (`CirclesController.Get`, `circle/[id].tsx` "Quem esta aqui"). Nao ha tela de descoberta com feed de pessoas antes de entrar. |
| Metricas do perfil acompanham o uso | feito e testado | 36 | Recap so dos fluxos usados. Ver secao Rodada 7. |
| Diario estilo Notion no solo, sem busca, sem versao, sem PDF, sync ao abrir, exporta texto | parcial | 243 a 250 | Area Diario com texto e export `.txt` (`LifeAreasController` export, `Diario_ExportaTextoCorrido`). Sem sessoes tipo Notion. Cache offline cobre o perfil; o diario em si nao tem fila propria. Export no app so dispara download se `document` existir (web). |
| Identidade (nome, foto) | feito e testado | abertura, 1 | Perfil + foto. Testes de auth/perfil/foto. Camera e galeria no perfil (`profile.tsx`); Playwright nao exercita esses botoes. |
| Tipo pet ou grupo no perfil | feito e testado | abertura (fala do entrevistador); 224 | `LivingProfile` (person/pet/group) alem da conta pessoa. Ver secao Rodada 6. Login proprio do pet nao cravado. |
| A mesma entrada em varios circulos sem duplicar | nao feito | abertura | `LifeEntry` e do perfil; `CirclePost` e outro objeto. |
| Feed geral tipo rede social | sem confirmacao | 20 | Transcricao: sem resposta direta visivel. |

## Documentos e fotos

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Documentos no perfil solo | feito e testado | 31 | `/api/documents`, `documents.tsx`. `Dono_EnviaListaBaixaEApagaDocumento`. |
| Criptografados no servidor, chave do app | feito e testado | 31 reformulada, 57, 57 reformulada | `EncryptedFileStorage` AES-256-GCM. `ArquivoNoDisco_NaoContemOTextoOriginal`, `ChaveVemDaConfiguracao_NaoDeUmCampoDeBanco`, `ChaveDeCifra_NaoFicaNoBanco`. |
| Recuperacao: login em qualquer aparelho, esqueci senha | feito e testado | 32, 33 | `forgot-password` / `reset-password`. `EsqueciSenha_SempreNoContentENaoVazaSeOEmailExiste`, `TokenDoEmail_RedefineASenhaUmaVez`. Playwright `esqueci a senha avisa que o email saiu`. E-mail vai para `sent_emails`, nao SMTP. |
| 2FA fora do inicio | feito e testado (rodada 5) | 34; pedido da rodada 5 | TOTP opcional. Ver secao Rodada 5. SMS nao existe. |
| Sem exportar cifrado para a pessoa abrir | feito sem teste da negativa | 58, 59 | Download autenticado devolve o arquivo em claro para o dono (`DocumentsController`). Nao ha endpoint de dump cifrado. |
| Backup pessoal do que e da pessoa num circulo | feito e testado | 7 | `GET /api/export/me` e `/api/export/circles/{id}`. Diario `.txt` segue existindo. Ver secao Rodada 5. |
| Foto comprime, cifra, substitui original | feito e testado | 56, 57, 273, 274 | `ImageCompressor` + `EncryptedFileStorage`. `Foto_ComprimeSubstituiOriginalEFicaCifrada`. Qualidade 82 e o valor no codigo. |
| Sem limite de tamanho no upload | nao feito | 272 | `FileRules.MaxIncomingBytes = 80 MB`. |
| Entradas: foto, texto, numero; sem video | feito e testado | 53, 54, 55 | `life/[id].tsx` tem numero e foto. Playwright `entrada guarda numero e o log do circulo mostra data`. Video nao aparece. |
| Foto por camera, galeria ou arquivo | feito e testado | 54, 55 | Perfil: camera e galeria. Documentos: `DocumentPicker`. Entrada: camera, galeria e arquivo na web. |
| 100 MB gratis / 1 GB pago; aviso a 90% | parcial | 275, 277 a 283 | `PlanLimits`. `Plano_MostraLimitesSemGateway`, `Armazenamento_DoisEnviosSobrepostosRespeitamOTeto`. Home mostra aviso de storage. Grafico de uso sem tipo: Ajustes + Playwright `backup do perfil e grafico de armazenamento` (279, 280). Sem limpeza em lote (281; dono pediu para sair do escopo). Excluir foto: `DELETE /api/profile/photo` e botao `Remover foto` em `profile.tsx`. Playwright `remover foto de perfil`. |

## Seguranca, auth e pipeline

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| JWT | feito e testado | 69 reinicio | `JwtTokenService`, testes de login/me. |
| Access 15 min + refresh 7 dias | feito e testado | 70, 71 (sugestao; rodada 5 pede) | `AccessTokenMinutes: 15`, `RefreshTokenDays: 7`. Ver secao Rodada 5. |
| Cookie httpOnly | nao feito | 72 (sugestao sem Ok/Beleza) | nao cravado. Bearer + storage. |
| WebSocket com TLS | nao feito | 73, 74 | Grep em `backend/src`: zero `UseWebSockets` / `SignalR`. |
| Estatisticas anonimas so internas, nunca vendidas | nao feito | 43, 44 | Sem coleta. |
| Repositorio privado | feito sem teste | 83 | API GitHub desta rodada: `private: true` em `allanmeneses/minhavida`. |
| GitHub Actions: build, unit, integracao | feito e testado | 79, 80, 81, 84 | `.github/workflows/ci.yml`: `dotnet test`, `npm test`, `tsc --noEmit`, Playwright. Jobs `backend` e `app` verdes no run desta branch. |
| Checagem de seguranca / Dependabot | parcial | 82 | `.github/dependabot.yml` nuget/npm/github-actions. CodeQL analisa; upload de code scanning nao ligado no repo. Ver secao Rodada 5. `dotnet list --vulnerable` nao entra no CI. |
| Testes desde o inicio; nada sobe sem teste | parcial | 79, 80 | xUnit 86 `[Fact]`, `npm test` 18 casos, Playwright 26. Nao ha gate de deploy. Deploy nao foi feito. |
| Edicao substitui de vez, sem versao | feito e testado | 68, 67 | PUT da entrada sobrescreve (`LifeAreasController.UpdateEntry`). Teste `Entrada_EdicaoSubstituiDeVez`. Sem tabela de versoes. |
| Tags opcionais nas entradas | nao feito | abertura | |

## Circulos

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Dono fixo que pode mudar; historico fica | feito e testado | 2, 3 | POST `/api/circles/{id}/transfer` com `UserRowLock`. Historico e permissoes ficam. Testes `Dono_TransfereEmVida_HistoricoEPermissoesFicam` e `TransferenciaSobreposta_SoUmaVence`. Playwright `transferir dono do circulo em vida`. |
| Varios gestores | parcial | 3 | Papel `Admin` existe (`CircleRole`). Nao ha varios donos simultaneos. |
| Sem dono ativo em caso sensivel: trava | parcial | 4 | `CircleStatus.LockedNoOwner` na exclusao sem admin/sucessor. Teste `Exclusao_SemAdminNemSucessor_TravaCirculo`. Sem trava por inatividade. |
| Circulo nao some na exclusao: transfere ou trava; se nao houver sucessor, votacao | parcial | 327 a 331, 330 | Transfere para admin: testado. Lock: `Exclusao_SemAdminNemSucessor_TravaCirculo`. Votacao nao existe (331 a 344; 336 sem confirmacao). |
| Votacao de sucessao (10%, candidaturas, empate, etc.) | nao feito | 331 a 344; 336 sem confirmacao | Sem entidades de voto. README coloca votacao no depois. 336 e 345 nao cravam regra. |
| Vida util temporario / duradouro / diario | feito e testado | 16 | Enum `CircleKind` + UI `Tipo:`. Arquivar e acao do admin, sem relogio. Ver secao Rodada 7. |
| Temporario encerrado: arquivo + retrospectiva | feito e testado | 17, 18 | `POST /archive` e recap. Sem data automatica de fim. Ver secao Rodada 7. |
| Compartilhar retrospectiva so a narrativa da pessoa | feito e testado | 19, 20 | Snapshot em `recap_shares`. Ver secao Rodada 7. |
| Arquivamento como beneficio de plano; retencao indefinida | nao feito | 26, 27 | Nao ha compactacao nem exclusao por tempo. Retencao de fato e "fica no banco"; nao ha politica escrita no codigo. |
| Cadastro livre; entrar em circulo alheio por pedido ou convite | feito e testado | 8 | Convite: `Dono_CriaConviteEOutroAceita`. Pedido: UI `Pedir para entrar` e lista no dono. Playwright `descobrir circulo e pedir entrada`. |
| Publico: quem nao entrou ve so nome e descricao | feito e testado | 9 | `Publico_VisitanteVeSoNomeEDescricao`. |
| Privado: so admins controlam permanencia | feito e testado | 8 | Aceitar pedido exige `Role >= Admin`. UI no detalhe. Playwright `descobrir circulo e pedir entrada`. |
| Busca por interesse e, com opt-in, por localizacao | parcial | 10, 11, 12, 12 reformulada | GET `/api/circles/discover?q=` e tela `Descobrir circulos`. Sem geo (localizacao nao cravada nesta rodada). |
| Circulos que nao querem ser encontrados: invisivel por padrao | feito e testado | 12 | `Discoverable` false se omitido. UI: `Oculto na busca` / `Aparece na busca`. Playwright cria com o botao. |
| Feed de descoberta interno, da para silenciar | nao feito | 37, 38, 39 | Discover lista circulos; nao e feed e nao ha silenciar. |
| Entrada apagada deixa rastro com quem e quando | feito e testado | 5, 6 | `EntradaApagada_DeixaRastroNoLog`. Tela formata `CreatedAt` com `formatPersonTime`. Playwright espera data no log. |
| Log visivel para o circulo, para sempre, fuso da pessoa | parcial | 267 a 271 | GET log no detalhe. UI mostra data/hora no fuso do aparelho. Sem retencao. Sem filtro (270 foi sugestao sem Ok; nao cravar). |
| Circulo surpresa: aniversariante nao ve; revelacao; recordacao no perfil; sair sem rastro | feito e testado | 229 reformulada a 236, 238 | `Surpresa_AniversarianteNaoVeOCirculo`, `Sair_NaoDeixaRastroDeParticipacao`. Revelacao, resumo e recordacao: ver secao Rodada 6. Revelacao por data automatica nao cravada (230). |
| Lista de ideias compartilhada: a outra pessoa pode incluir? | sem confirmacao | 229 primeira formula (cortada) | Lembrete tem `giftIdeas` so no dono. Nao ha compartilhar no circulo (228). |
| Templates de rotina | feito e testado | 47 a 52 | Catalogo pt-BR, copia editavel, uso/ranking, criador anonimo. Ver secao Rodada 6. |
| Denuncia interna, bloqueio pela equipe, recurso humano | nao feito | 21 a 25 | |
| Papeis so ver / comentar / editar | parcial | abertura | Enum Viewer/Commenter/Editor/Admin/Owner. Convite aceita `role`. Posts exigem Editor. Commenter nao tem endpoint de comentario. |

## Publicacoes, painel, busca, offline

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Painel do dia primeiro: vence, atrasado, em seguida, quantos circulos | feito e testado | 251 a 254 | `GET /api/home`, `home.tsx`. Poll a cada 5s e ao voltar o foco (sem WebSocket; 73 e 74 nao feitos). Playwright `painel do dia atualiza sozinho`. |
| Botao + no painel sugerindo contexto | nao feito | 255, 256 (sem Ok) | |
| Busca unificada + historico sincronizado | feito e testado | 257 a 260 | `GET /api/search` e `/api/search/history`. Tela `search.tsx`. Teste `Busca_UnificadaComHistoricoSincronizadoETermosFrequentes`. Playwright `busca unificada com historico`. |
| Offline: guarda local, ultima sync vence, icone online/offline | parcial | 261 a 266 | Cache + fila de listas + icone. Conflito entre aparelhos (262) nao cravado. Ver secao Rodada 5. |

## Lembretes e recorrencia

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Lembrete e funcao do app, nao de outro calendario | feito e testado | 45, 46, 223 | CRUD em `/api/reminders`. Calendario proprio no app/web. Nao cria evento no aparelho. Ver secao Rodada 6. |
| Integracao calendario so leitura, se existir | parcial | 223 | Visao e exportacao `.ics` (assinatura/download). Sem gravar no calendario nativo do aparelho (46). |
| E-mail de lembrete opcional por pessoa | nao feito | 13, 14 | `OutboxEmailSender` so em convite, esqueci senha e exclusao. Sem preferencia de e-mail no lembrete. Sem disparo agendado. |
| Circulo ciclico com atalho que marca feito | feito e testado | 215 | `Shortcut.ReminderId`. `Atalho_MarcaLembreteDeCirculoComoFeito`. UI liga o atalho ao lembrete do circulo. Playwright `atalho marca lembrete de circulo como feito`. |
| Recorrencia tipo Teams | feito e testado | 216 | Calculadora + UI: none/daily/weekly/monthly/yearly, intervalo, dias da semana, mensal por data ou posicao, until, maxOccurrences. Teste `Anual_MesmoDiaDoAnoSeguinte`. Playwright `lembrete anual aparece na tela`. |
| Pausa sem perder historico; ao retomar segue a data original | feito e testado | 217 a 221 | `Pausa_MantemProximoDisparoDaGrade`. Skip: `Skip_AvancaParaAProximaOcorrencia` e botao `Pular`. Playwright `pular lembrete e aviso de recorrencia acabando`. |
| Aviso de fim (ultimos 3) | feito e testado | 222 | `endingSoon`. Teste `RecorrenciaComPoucoRestante_MarcaEndingSoon`. UI: `Recorrencia acabando`. |
| Aniversario com antecedencia, foto, ideias de presente | parcial | 222, 224 a 228 | `Aniversario_ComAntecedenciaEListaDeIdeias`. Playwright cria aniversario. `Reminder.PhotoStorageKey` existe; nao ha endpoint de foto do lembrete. Ideias nao compartilham no circulo. |
| Modo ferias silencia; ao voltar so o que ainda faz sentido | feito e testado | 239 a 242 | `Ferias_AceitaPrazoEscolhidoPelaPessoa`. UI: campo `Dias de ferias`. Playwright `ferias com prazo escolhido`. |

## Atalhos

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Atalhos de uma acao, varios na tela | feito e testado | 98, 102, 103 | Home lista e dispara. `Atalho_ExecutaSemConfirmacaoEApareceNoPainel`. Playwright `atalho de um toque dispara sem confirmacao`. |
| Posicionaveis livremente | parcial | 102, 103 | Reordena com Subir/Descer e `PUT /api/shortcuts/order`. Teste `Atalho_ReordenaNaTela`. Playwright `atalhos reordenam na tela`. Sem arrastar livre (103). |
| Icone na tela inicial e tecla no computador | nao feito | 100 | Sem widget, sem atalho de teclado, sem PWA install. 99 ficou sem resposta direta; vale a 100. |
| Sem gesto de balancar | feito (por ausencia) | 100 | Nao ha gesto. |
| Executa direto, sem confirmacao; so o nome | feito e testado | 101, 104 a 107 | POST `/run` sem dialog. |
| Feedback 1 segundo | nao feito | 105 (sem Ok) | `flash` do label permanece ate novo load. |
| Cor por tipo de acao | parcial | 106, 107 | UI cria com `#C45C26`. Sem paleta por tipo. API default se omitir cor: `#1F6F66`. |
| Som e vibracao opcionais | feito e testado | 109, 200, 201 | `playShortcutFeedback`: bip 880 Hz 0,12s e vibracao 40 ms. `npm test` `usa bip padrao e vibracao curta`. Playwright nao mede audio. |
| Desativar some da principal; excluir mantem historico | parcial | 202 a 207 | UI Desativar/Reativar/Excluir. Playwright `atalho desativado some da tela principal e pode voltar`. Recuperar depois de excluir (207) nao cravar: resposta "Pode ser alterado". |
| Limite 5 gratis / 20 ativos | nao feito | 108, 208 (sem Ok) | Sem teto. |
| Compartilhar atalho no circulo | nao feito | 209 a 214 | |

## Notificacoes e interface

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Tempo real, pessoa controla o jeito | nao feito | 40, 40 reformulada | Sem push, sem WebSocket. Painel so no GET. Anuncios vs organico (40 original) sem resposta. |
| Interface muda (sem som ao salvar) | feito sem teste | 287 | Sem som no save. |
| Dark mode automatico com override | feito e testado (web) | 41, 284 | `userInterfaceStyle: automatic`, `ThemeContext`. Playwright `modo escuro troca a paleta na hora`. |
| Acessibilidade leitor e contraste alto | parcial | 42 | Contraste alto: feito e testado (Rodada 7). `accessibilityLabel` em `Button` e `TextField`. Sem teste de leitor. |
| Uma lingua, estrutura para mais | parcial | 35 | Textos em portugues no codigo. Sem i18n (chaves/arquivos). |
| Transicao instantanea | nao verificado | 286 | Nao medi animacao. Expo Router usa transicao padrao. |
| Nome e dominio no fim | parcial | 90, 91, 92 | O app ja se chama Circulos (`app.json`, JWT Issuer). Sem dominio de producao. |

## Privacidade e exclusao

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Termo e politica no dia um | feito e testado | 60 | Paginas `/termos` e `/privacidade` em portugues, responsavel ALLAN WANDERSON MENESES DE SOUSA, sem CNPJ. Playwright `termos e privacidade no app`. |
| Adequacao LGPD: revogacao/exclusao | parcial | 61, 62, 63 | Exclusao de conta existe. Termos e privacidade descrevem o que o app faz. Sem painel do titular (69). |
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
| FAQ no menu, tour na primeira abertura | feito e testado | 288 a 299 | Telas `faq.tsx` e `TourGuide`. Ver secao Rodada 5. |

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
| Observabilidade gratuita (OTel + Grafana, sem Ok) | feito e testado | 77, 78 | OpenTelemetry traces e metricas, desligado por padrao. Grafana Cloud nao foi ligado. Ver secao Rodada 6. |
| Pagina de status (UptimeRobot registrado pelo entrevistador) | feito e testado | 87, 88, 89 | `/health`, `/api/status` e HTML `/status`. Cadastro no UptimeRobot nao foi feito daqui. Ver secao Rodada 6. |
| Conventional Commits | nao feito | 85, 86 | Mensagens curtas nesta branch, sem prefixo `feat:`/`fix:`. Semver: dono indiferente. |
| PWA com atalho na tela inicial | nao feito | abertura | Sem manifesto PWA, sem service worker. |
| TokTok Aluguel / limite do bot | fora de escopo | abertura | Outro produto. |
| Quem desenvolve / tempo por semana | fora de escopo de codigo | 93, 94 | Processo, nao produto. |

## App Android desta rodada

- Prints em `/opt/cursor/artifacts/android-login.png`, `android-hoje.png`, `android-circulo.png`, `android-lembrete.png` (existem neste disco). Nao reexecutei o emulador nesta continuacao da analise.
- `href: null` nas tabs escondidas impedia abrir Lembretes e o detalhe do circulo no nativo; o codigo agora usa `tabBarButton: () => null`. Sem teste automatizado nativo disso.
- HTTP em texto puro: plugin `withCleartextDebugOnly` trava release (`usesCleartextTraffic=false`) e libera so `app/src/debug`. Teste `nao libera HTTP na configuracao de producao do app.json`. CI grepa os dois manifests apos o prebuild.
- Job `android` do run [45038488241](https://github.com/allanmeneses/minhavida/actions/runs/45038488241) (SHA `5c0e6ef`): conclusion `success`. Step `Gerar APK` 15:28:44 a 15:57:50 UTC. Step `Publicar APK` `success`. Artefato `circulos-android` (id `14762035932`, 20927286 bytes, `expired: false`). Download na pagina do run. Jobs `backend` e `app` do mesmo run: `success`. Run inteiro: `success`.

## Contagem

Nao some "feito por ausencia" com entrega de produto. Os buracos que ainda impedem o combinado da ligacao estao nas linhas **parcial** e **nao feito**.

## Listas compartilhadas (pedido 22/11/2026)

| Decisao | Status | Origem | Evidencia |
| --- | --- | --- | --- |
| Lista no perfil solo ou no circulo, com tipo | feito e testado | pedido 22/11/2026 | `POST /api/lists`. Telas `lists.tsx`, `circle/[id].tsx`. `Solo_CriaListaAdicionaItemMarcaReordenaEExclui`. Playwright `lista de compras no perfil`. |
| Itens com texto, qtd, nota, feito, quem, ordem, exclusao | feito e testado | pedido 22/11/2026 | `SharedListItem`. Teste solo acima. |
| Membros veem ao atualizar, inclusive escrita sobreposta | feito e testado | pedido 22/11/2026 | `Circulo_DoisMembrosVeemAMesmaListaMesmoComEscritaSobreposta`. Tela da lista faz poll de 5s. |
| Permissao por lista (view/add/complete/manage), alteravel, 403 | feito e testado | complemento 22/11/2026 | `PUT /api/lists/{id}/access`. Testes `PermissaoView_MembroSoVe_403NoResto`, `PermissaoAdd_MembroAdiciona_403NoDone`, `PermissaoComplete_MembroMarca_403NoEditar`, `PermissaoManage_MembroEditaEExclui`, `PermissaoAlteradaDepois_PassaAValer`. Playwright `permissao da lista: so ver e depois marcar feito`. |
| Override por membro | feito e testado | complemento 22/11/2026 | `PUT /api/lists/{id}/grants`. `PermissaoPorMembro_SobrescreveOPadrao`. |
| Desejos escondidos do homenageado na surpresa | feito e testado | pedido 22/11/2026 | `Surpresa_HomenageadoNaoVeListaDeDesejos`. |
| Criar lista no circulo respeita papel Editor+ | feito e testado | pedido 22/11/2026 | `Viewer_NaoCriaListaDoCirculo`. |

## Fora desta rodada (nao cravar)

- Busca por localizacao / geo (10 a 12 no detalhe do opt-in e se some depois).
- Recuperar atalho depois de excluir (207; resposta "Pode ser alterado").
- Filtro do log (270; sugestao sem Ok).
- Votacao de sucessao (331 a 344; 336 sem confirmacao).
- WebSocket (73, 74); o painel usa poll de 5s.
- Botao + no painel (255, 256; sugestao sem Ok).
- Arrastar atalho para qualquer ponto da tela (103).
- Cookie httpOnly (72; sugestao sem Ok). Bearer no app.
- Conflito offline entre dois aparelhos (262; ultima sincronizada vence, sem regra de merge).
- SMS para 2FA (34 cobriu so o recorte inicial; esta rodada fez TOTP).
- Billing/gateway, SMTP real, deploy Azure, iOS nativo.
- Revelacao surpresa agendada por data (230; a ligacao descreve o momento da revelacao, nao um relogio).
- Login proprio para pet/grupo; edicao do perfil vivo por qualquer membro.

A lista consolidada do que ainda pede o Allan esta na secao **O que ainda depende de decisao do Allan** (Rodada 7).
