# Circulos

Hub da vida pessoal. A pessoa cria circulos (amizade, familia, grupos de interesse) para compartilhar coisas e ficar por dentro do que o organizador publica. Nao ha nicho fixo: o app serve o dia a dia, a partir do perfil de quem usa.

Este repositorio e o projeto **minhavida**.

## Conceitos

1. **Perfil pessoal.** Ponto de partida de tudo. E tambem o lugar onde a pessoa guarda documentos privados.
2. **Rotinas de lembrete.** Exemplos abertos, como uma lista de aniversarios que avisa a pessoa. Fora da v1.
3. **Uso no dia a dia.** Se a pessoa quer lembrar de algo, abre o app e usa. Os exemplos nao limitam o produto.
4. **Circulos.** Espacos criados pela pessoa para familia, amizade ou interesse. O organizador publica; quem participa acompanha. Fora da v1.

O modelo de dados da v1 gira em torno de `User`. Perfil e documentos pessoais apontam para esse usuario. Circulos, membros e lembretes entram depois, sempre ancorados no mesmo `User`.

## O que a v1 faz

Somente o perfil pessoal:

- Cadastro e login com e-mail e senha
- Perfil com nome, foto, data de nascimento e contatos
- Area de documentos pessoais: enviar, listar, baixar e apagar arquivos privados

Circulos, lembretes e o restante ficam no roadmap. Nao ha URL publica permanente para arquivo. Foto e documento so saem pelo backend, com autenticacao, e so para o dono.

## Roadmap

- **v1 (esta):** conta, perfil e documentos privados
- **v2:** lembretes pessoais (aniversarios e avisos simples)
- **v3:** circulos, convites e publicacoes do organizador
- **depois:** notificacoes, compartilhamento controlado e uso cotidiano mais amplo

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
| Testes | xUnit + `WebApplicationFactory` | Auth e documentos isolados, sem Docker na suíte |

Nao usamos um segundo cliente web. O Expo no navegador e a cara de web; no celular, a cara de app nativo.

## Como rodar local

Requisitos: .NET 10 SDK, Node 22+, Docker (para o Postgres) ou um PostgreSQL 16 na porta 5432.

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

A API aplica a migracao na subida. Saude: `http://127.0.0.1:5288/health`.

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
