# Planos do Circulos

Status: **PROPOSTA** para o Allan aprovar. Precos em reais. Os valores nao ficam fixos no codigo da cobranca: vêm de `Plans:PaidMonthlyReais` e `Plans:PaidYearlyReais` (arquivo de configuracao ou secret do ambiente).

## Gratis

- Ate 3 circulos criados pela pessoa
- 100 MB de armazenamento
- 5 atalhos
- Circulos e historico continuam se a pessoa deixar de pagar depois

## Pago mensal

- Valor proposto: 29,90 reais por mes (configuravel)
- Circulos ilimitados
- 1 GB de armazenamento
- 20 atalhos
- Cartao recorrente no Mercado Pago

## Pago anual

- Valor proposto: 299,00 reais por ano (configuravel)
- Mesmos limites do pago mensal
- Cobranca anual no mesmo gateway

## Inadimplencia

Se a assinatura suspender: atalhos extras, armazenamento acima do teto gratis e compartilhar retrospectiva bloqueiam. Circulos e historico ficam. Cancelar deixa o acesso pago ate o fim do ciclo (`PaidUntil`).

## Gateway

Mercado Pago, preapproval de cartao, webhook com HMAC (`id` + `request-id` + `ts`). Credenciais so por variavel de ambiente. `Billing:Enabled` desligado por padrao. `Billing:Sandbox` ligado. Sem AccessToken, o app usa o gateway simulado, sem chamada real.
