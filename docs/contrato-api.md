# Contrato — Serviço de Suporte

Para quem chama o serviço (hoje o `order`). Rede interna do cluster: `http://support:8090`. Fora do API Gateway
e sem Ingress. Todas as rotas `/internal/*` exigem o cabeçalho `x-internal-secret`.

## `POST /internal/notifications`

Cabeçalhos: `x-internal-secret` (obrigatório), `Content-Type: application/json`, `X-Correlation-ID` (opcional).

Corpo (`OrderCreated`, base em `order/docs/INTEGRATION-CONTRACTS.md` §8):

```json
{
  "event_id": "0b6f3a3e-8d0e-4c34-9d36-4f0f2f5b9a11",
  "event_type": "OrderCreated",
  "occurred_at": "2026-10-19T14:30:00Z",
  "order_id": "7",
  "order_code": "GANJJ-000007",
  "customer_id": "42",
  "customer_email": "cliente@example.com",
  "customer_phone": "+5541999999999",
  "total": 1234.56,
  "currency": "BRL",
  "correlation_id": "6a2f41a5-c3f9-4d0f-a8cb-0a6e8a8e1a11"
}
```

| Campo | Tipo | Obrigatório | Regra |
|---|---|---|---|
| `event_id` | string (UUID) | sim | Chave de idempotência |
| `event_type` | string | sim | Só `OrderCreated` |
| `occurred_at` | string RFC 3339 | sim | |
| `order_id`, `order_code`, `customer_id` | string | sim | |
| `customer_email` | string | sim | Formato de e-mail |
| `customer_phone` | string | não | E.164 (`+` e 8 a 15 dígitos); ausente: SMS e WhatsApp ficam `skipped` |
| `total` | número | sim | Maior ou igual a 0 |
| `currency` | string | sim | `BRL` |
| `correlation_id` | string | não | Vai para o log |

Respostas:

| Status | Corpo | Quando |
|---|---|---|
| 202 | `{"status":"accepted","event_id":"...","duplicate":false}` | Aceito; envio assíncrono |
| 202 | `{"status":"accepted","event_id":"...","duplicate":true}` | `event_id` já recebido; nada é reenviado |
| 400 | `{"error":"campo inválido: customer_email"}` | Validação (mensagem em pt-BR, campo nomeado) |
| 403 | `{"error":"acesso negado"}` | Segredo ausente ou errado |

O `order` **não deve falhar o pedido** se o suporte estiver fora ou responder erro; o aviso é efeito colateral.

## `GET /internal/notifications/{event_id}`

Status do evento e das entregas (para depuração e demo). Exige `x-internal-secret`.

```json
{
  "event_id": "...",
  "deliveries": [
    {"channel": "email", "status": "sent", "provider": "smtp", "attempts": 1},
    {"channel": "sms", "status": "sent", "provider": "fake", "attempts": 1},
    {"channel": "whatsapp", "status": "skipped", "provider": "fake", "attempts": 0}
  ]
}
```

`404 {"error":"evento não encontrado"}` se o `event_id` não existir.

## Operacionais (sem segredo)

| Rota | Resposta |
|---|---|
| `GET /health` | `200 {"status":"ok"}` (liveness, não depende de nada) |
| `GET /health/ready` | `200` com o SQLite aberto; `503` caso contrário |
| `GET /metrics` | Texto Prometheus |
