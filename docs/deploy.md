# O que falta para um deploy real

Esta rodada nao sobe o Circulos em producao. Abaixo e o que precisa existir antes de um primeiro ambiente compartilhado.

## API (.NET)

A ligacao apontou Azure. Um caminho direto:

1. Azure Database for PostgreSQL 16, com SSL obrigatorio.
2. Azure App Service ou Container Apps para `Circulos.Api`, apontando a connection string para esse banco.
3. Disco persistente ou Blob Storage no lugar de `App_Data/storage`. O codigo atual grava arquivo local via `LocalFileStorage`.
4. Segredos fora do repositorio:
   - `Jwt:Key` com pelo menos 32 caracteres, gerada para o ambiente
   - `Encryption:Key` em Base64, 32 bytes, diferente da chave de desenvolvimento em `appsettings.json`
   - connection string do Postgres
5. `Cors:Origins` com a URL real do app web.
6. `ASPNETCORE_ENVIRONMENT=Production`.
7. HTTPS na frente da API. O cliente so deve chamar essa URL.

A API aplica as migracoes na subida (`db.Database.Migrate()`). O usuario do banco precisa poder criar tabelas na primeira vez.

## E-mail

`OutboxEmailSender` grava em `sent_emails`. Esqueci senha e exclusao de conta dependem de um codigo que hoje nao sai por SMTP. Para uso real: SMTP (Azure Communication Services, SendGrid ou equivalente) no lugar desse outbox, sem mudar o contrato dos endpoints.

## App

- Web: `npx expo export --platform web` e um host estatico (Azure Static Web Apps, Blob + CDN). `EXPO_PUBLIC_API_URL` aponta para a API publica.
- Android: o APK desta rodada e o `assembleRelease` do prebuild Expo, assinado com o keystore de debug. O job `android` do CI publica o artefato `circulos-android` (`app-release.apk`). HTTP em texto puro fica so no build de debug/emulador (`app/src/debug`). O APK de release nao aceita HTTP. Loja pede keystore proprio, `applicationId` definitivo e EAS Submit ou Play Console.
- iOS: nao foi gerado nesta rodada. Precisa de conta Apple, certificados e `eas build` ou Xcode.

Num celular fisico, `EXPO_PUBLIC_API_URL=http://127.0.0.1:5288` nao funciona. Use o IP da maquina na LAN ou a URL publica HTTPS.

## Fora desta versao, mas citados na especificacao

- Deploy Azure e SMTP real (Allan, 22/11/2026: ainda nao)
- WebSocket com TLS
- Pagina de status (UptimeRobot ou similar)
- iOS nativo, PWA de loja, SMS 2FA

Billing desta rodada fica em sandbox, desligado por padrao. Para ligar Mercado Pago de verdade: `Billing:Enabled=true`, `Billing:Provider=MercadoPago`, `MercadoPago:AccessToken` e `MercadoPago:WebhookSecret` so em secret, nunca no repositorio.

Nenhum desses itens e requisito para o primeiro deploy da API + app atuais.
