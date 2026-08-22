# ADR 0003 - Use an orchestrated Saga, idempotency, and outbox

Status: Accepted  
Date: 2026-08-21

## Context

Stock, Cart, and Order are stored in different technologies and cannot share one
ACID transaction. Client/network retries may repeat unsafe HTTP operations. Cart
cleanup occurs only after the order transaction is durable.

## Decision

- Order orchestrates per-item stock decrement and reverse compensation.
- `POST /api/pedidos` requires an idempotency key stored in SQL Server.
- Product operations require stable operation identifiers.
- Order, items, and idempotency completion commit together.
- An outbox is introduced when a real post-commit action exists, such as safe Cart
  quote consumption or an approved `OrderCreated` event.

## Consequences

- Checkout is eventually consistent across services.
- Compensation and duplicate-request failure paths require first-class tests.
- Outbox processing is deferred in the interim current-Cart implementation.
- A failed compensation is operationally visible and may require recovery.
