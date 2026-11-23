# Circulos

Hub da vida pessoal. A pessoa cria circulos (amizade, familia, grupos de interesse) para compartilhar coisas e ficar por dentro do que o organizador publica. Nao ha nicho fixo: o app serve o dia a dia, a partir do perfil de quem usa.

Este repositorio e o projeto **minhavida**.

## Conceitos

1. **Perfil pessoal.** Ponto de partida de tudo. E tambem o lugar onde a pessoa guarda documentos privados.
2. **Rotinas de lembrete.** Exemplos abertos, como uma lista de aniversarios que avisa a pessoa. Fora da v1.
3. **Uso no dia a dia.** Se a pessoa quer lembrar de algo, abre o app e usa. Os exemplos nao limitam o produto.
4. **Circulos.** Espacos criados pela pessoa para familia, amizade ou interesse. O organizador publica; quem participa acompanha. Fora da v1.

O modelo gira em torno de `User`. Perfil, documentos, areas de vida, circulos, lembretes e atalhos apontam para esse usuario.

## O que esta versao faz

- Cadastro e login com e-mail e senha, mais esqueci a senha por e-mail
- Perfil solo privado (pode virar publico) com vacinas, lembretes, documentos e diario
- Documentos e fotos cifrados em AES-256-GCM no servidor; a chave fica fora do banco
- Circulos com dono, convite, publico/privado, surpresa e log
- Lembretes com recorrencia, aniversario e modo ferias
- Painel do dia, atalhos de um toque, tema claro/escuro automatico
- Foto pela camera, galeria ou arquivo, comprimida no padrao Instagram
- Exclusao de conta com senha e confirmacao por e-mail (LGPD)
- Limites do plano gratuito (3 circulos, 5 atalhos, 100 MB) e checkout Mercado Pago em sandbox (`PAYMENTS_PROVIDER` simulado por padrao)
- Busca por raio so de circulos, com a posicao da pessoa so na hora (sem gravar latitude nem longitude)
- Lembretes do circulo para membros, no app e por e-mail se ligado; sem SMS
- Diario com busca por palavra-chave; ideias de presente editaveis por editor, so leitura para viewer
- Votacao de sucessao por maioria simples, sem quorum; dono eleito so substitui apos 30 dias

Nao ha URL publica permanente para arquivo. Foto e documento so saem pelo backend, com autenticacao. Nao ha feed geral nem anuncios. O app le o calendario do aparelho e nunca grava evento la.

Mensagens de commit: curtas em pt-BR, ver `docs/contribuicao.md`.

## Roadmap

- **feito aqui:** conta, perfil solo, documentos cifrados, circulos, lembretes, atalhos, tema, compressao, esqueci senha, exclusao LGPD, limites de plano
- **depois:** SMTP real, deploy Azure, iOS nativo, WebSocket, PWA de loja

## Stack e por que

A sugestao do dono foi ASP.NET e um cliente unico Expo. Mantivemos isso. Ajuste pontual: **.NET 10**, nao .NET 8. O piso era 8+, e em novembro de 2026 o LTS vigente e o 10.

| Parte | Escolha | Motivo |
| --- | --- | --- |
| API | ASP.NET Core 10 | Baixa manutencao, alinhada a quem ja trabalha com C# |
| Dados | PostgreSQL 16 + EF Core | Banco relacional simples, migracoes versionadas |
| Auth | JWT + `PasswordHasher` | E-mail e senha sem Identity UI e sem tabelas extras |
| Arquivos | Disco local, chave interna | Privacidade por usuario; download so autenticado |
| Cliente | Expo SDK 58 + Expo Router | Um codigo para iOS, Android e web |
| Visual | Componentes por plataforma | Abas nativas no celular; barra lateral e tabela na web |
| Testes | xUnit + `WebApplicationFactory` | Integracao e E2E contra PostgreSQL 16 |

Nao usamos um segundo cliente web. O Expo no navegador e a cara de web; no celular, a cara de app nativo.

## Como rodar local

Requisitos: .NET 10 SDK, Node 22+, Docker (para o Postgres) ou um PostgreSQL 16 na porta 5432. Sem Docker, da para subir a API com SQLite.

### 1. Banco

```bash
docker compose up -d
```

Usuario, senha e banco: `circulos`. Porta `5432`.

### 2. API

```bash
cd backend
dotnet test
dotnet run --project src/Circulos.Api --urls http://127.0.0.1:5288
```

`dotnet test` e o Playwright web sobem contra o Postgres local (usuario/senha/banco `circulos`). No GitHub Actions o workflow usa um service container `postgres:16-alpine`.

A API aplica a migracao na subida. Saude: `http://127.0.0.1:5288/health`.

Sem Postgres:

```bash
Database__Provider=Sqlite ConnectionStrings__Default="Data Source=/tmp/circulos.db" \
  dotnet run --project src/Circulos.Api --urls http://127.0.0.1:5288
```

A chave JWT em `appsettings.json` e so para desenvolvimento local. Troque antes de qualquer ambiente compartilhado.

### 3. App (web, iOS ou Android)

```bash
cd app
npm install
npx expo start --web --port 43123
```

O cliente lê `EXPO_PUBLIC_API_URL` (padrao `http://127.0.0.1:5288`, tambem em `app/.env`).

- **Web:** o Metro abre o navegador com layout de site (sidebar, tabela de documentos).
- **Celular:** `npx expo start` e o app Expo Go, com abas nativas.
- Num dispositivo fisico, aponte `EXPO_PUBLIC_API_URL` para o IP da maquina, nao para `127.0.0.1`.

## Estrutura

```
backend/src/Circulos.Api     API HTTP
backend/tests/Circulos.Api.Tests
app/                        Cliente Expo (iOS, Android, web)
docker-compose.yml          Postgres local
```

## Privacidade dos documentos

- Cada linha em `personal_documents` tem `UserId`. Listar, baixar e apagar filtram por esse id.
- Se o id nao e da pessoa autenticada, a API responde 404. Nao confirma que o arquivo existe.
- A chave de armazenamento nunca vai para o cliente.
- Documentos e fotos entram cifrados em AES-256-GCM. A chave fica em `Encryption:Key` (configuracao), fora do banco.
- Download e foto usam `Cache-Control: private, no-store`.
- Sem bucket publico e sem URL assinada de longa duracao nesta versao.
