# Planos do Circulos

Status: **aprovado** pelo Allan em 22/11/2026. Precos em reais: R$ 29,90 mensal e R$ 299,00 anual. Os valores nao ficam fixos no codigo da cobranca: vêm de `Plans:PaidMonthlyReais` e `Plans:PaidYearlyReais` (arquivo de configuracao ou secret do ambiente). Padrao no codigo: 29,90 e 299,00.

Nenhum plano inclui anuncios. Nunca havera anuncios no Circulos. Tambem nao existe feed geral tipo rede social.

## Gratis

- Ate 3 circulos criados pela pessoa. Eu e Familia do primeiro acesso nao entram nessa cota.
- 100 MB de armazenamento
- 5 atalhos
- Circulos e historico continuam se a pessoa deixar de pagar depois

## Pago mensal

- Valor aprovado: 29,90 reais por mes (configuravel)
- Circulos ilimitados
- 1 GB de armazenamento
- 20 atalhos
- Cartao recorrente no Mercado Pago

## Pago anual

- Valor aprovado: 299,00 reais por ano (configuravel)
- Mesmos limites do pago mensal
- Cobranca anual no mesmo gateway

## Inadimplencia

Se a assinatura suspender: atalhos extras, armazenamento acima do teto gratis e compartilhar retrospectiva bloqueiam. Circulos e historico ficam. Cancelar deixa o acesso pago ate o fim do ciclo (`PaidUntil`).

## Gateway

Mercado Pago, preapproval de cartao, webhook com HMAC (`id` + `request-id` + `ts`). O gateway tambem gera Pix avulso (`/v1/payments`, `X-Idempotency-Key`). Credenciais so por variavel de ambiente (`PAYMENTS_PROVIDER`, `MP_ACCESS_TOKEN`, `MP_WEBHOOK_SECRET`, `PUBLIC_BASE_URL`). `PAYMENTS_PROVIDER` padrao: simulado. `Billing:Enabled` desligado por padrao. `Billing:Sandbox` ligado. Roteiro de sandbox em `docs/roteiro-mercado-pago.md`.
