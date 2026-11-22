# Roteiro de teste com o Mercado Pago real

Os testes automaticos usam o gateway simulado (`PAYMENTS_PROVIDER=Simulated`). Este roteiro e a prova, feita uma vez antes de ligar cobranca de verdade e de novo a cada mudanca na integracao, de que o Circulos fala com o Mercado Pago de sandbox. Cada passo diz o que fazer, o que deve acontecer e onde conferir.

O que este roteiro afirma sobre o **nosso** sistema foi conferido no codigo (rotas `/preapproval`, `/v1/payments`, webhook HMAC, `notification_url` so com https). O que ele diz sobre o **painel e as contas de teste do Mercado Pago** vem da documentacao publica deles e nao foi verificado por mim; se o painel estiver diferente, siga a documentacao atual do Mercado Pago.

## O que voce precisa

1. **Duas contas de teste** no painel de desenvolvedor do Mercado Pago (Suas integracoes, Contas de teste): uma **vendedora** (as credenciais da aplicacao do Circulos) e uma **compradora** (quem paga a assinatura).
2. **Uma aplicacao** no painel, com:
   - credenciais de teste: Access Token (e, se o painel pedir, Public Key);
   - webhook `https://SEU-ENDERECO/webhooks/mercadopago` com os eventos de **Pagamentos** e **Planos e assinaturas**, e a **assinatura secreta** copiada.
3. **Um endereco https publico** que chegue na API. O sistema so manda `notification_url` ao Mercado Pago quando `PUBLIC_BASE_URL` (ou `PublicBaseUrl`) comeca com `https://`. Sem isso, nenhum webhook chega. Localmente, use um tunel (por exemplo, `cloudflared tunnel --url http://127.0.0.1:5288`) e use o endereco gerado como `SEU-ENDERECO`.
4. Cartao de teste da documentacao do Mercado Pago para autorizar a preapproval (nao verificado por mim neste ambiente).

Nenhuma credencial, token ou dado de cliente entra neste repositorio.

## Subir a API com o Mercado Pago

Variaveis so no ambiente (ou secret do host), nunca no Git:

```dotenv
PAYMENTS_PROVIDER=MercadoPago
PUBLIC_BASE_URL=https://SEU-ENDERECO
MP_ACCESS_TOKEN=...
MP_WEBHOOK_SECRET=...
Billing__Enabled=true
Billing__Sandbox=true
```

`Payments:Provider` aceita o mesmo valor. Padrao, se nada for definido: simulado.

Antes de comecar, confira:

- **0.1** `https://SEU-ENDERECO/health` responde 200.
- **0.2** `https://SEU-ENDERECO/dev/rotina` responde **404**: com o Mercado Pago, as ferramentas de simulacao nao existem.

## 1. Assinatura recorrente (preapproval)

1. Abra `/pricing`, entre com uma conta do Circulos e clique em assinar mensal (sandbox).
2. Conclua o checkout do Mercado Pago com a **conta compradora de teste** e um cartao de teste.

Esperado:

- A API chama `POST https://api.mercadopago.com/preapproval` com `back_url`, `external_reference` (id da pessoa), `auto_recurring` em BRL e `notification_url` apontando para `/webhooks/mercadopago?source_news=webhooks`.
- Ajustes mostra plano pago e status `active` (ou `pending` ate o webhook).
- O Circulos consulta `GET /preapproval/{id}` no webhook, sem confiar no corpo do aviso.

Cancelar no app chama `PUT /preapproval/{id}` com `status=cancelled`. O acesso pago segue ate `PaidUntil`.

## 2. Pix avulso (gateway)

O Circulos cobra o plano por cartao recorrente. O gateway tambem gera Pix (`POST /v1/payments` com `payment_method_id=pix` e cabecalho `X-Idempotency-Key` em toda criacao) e consulta `GET /v1/payments/{id}`. Nao ha tela de Pix no app nesta rodada. Para provar o caminho, use um teste automatico (`Pix_CriaComIdempotencyKeyEConsultaPagamento`) ou uma chamada autenticada de desenvolvimento contra o gateway simulado. Com `PAYMENTS_PROVIDER=MercadoPago`, nao use `/dev/rotina`.

A forma de aprovar um Pix no sandbox depende do Mercado Pago; confira na documentacao deles. Nao verificado por mim.

## 3. Webhook forjado e recusado

```bash
curl -s -o /tmp/mp-webhook.txt -w "%{http_code}" -X POST "https://SEU-ENDERECO/webhooks/mercadopago?type=subscription_preapproval&data.id=123" \
  -H "content-type: application/json" \
  -d "{}"
```

Esperado: **401**. Assinatura invalida nao processa e nao consulta a API. O manifesto do HMAC e `id:{data.id};request-id:{x-request-id};ts:{ts};` (documentacao publica de webhooks do Mercado Pago). Mesmo com assinatura valida, o Circulos reconsulta preapproval ou pagamento antes de mudar o status do plano. O mesmo `x-request-id` nao grava o evento duas vezes.

## 4. Encerrar

Desligue o tunel. Tire `PAYMENTS_PROVIDER=MercadoPago` e as credenciais do ambiente. Volte ao simulado.

## Resultado

| Passo | O que provou | OK? | Observacao |
| --- | --- | --- | --- |
| 0.1-0.2 | API no ar, sem ferramentas de simulacao | | |
| 1 | Preapproval criada, consultada e cancelada | | |
| 2 | Pix no gateway (se exercitado) | | Nao verificado neste ambiente |
| 3 | Webhook forjado recusado (401) e reconsulta | | |
