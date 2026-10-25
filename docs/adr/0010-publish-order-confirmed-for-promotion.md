# ADR 0010 - Publish OrderConfirmed for Promotion

Status: Accepted contract; implementation pending

Date: 2026-10-25

Order and Promotion use a durable RabbitMQ direct exchange for the
`OrderConfirmed` event. Although internal REST already satisfies the course
rubric, its coupon-consume endpoint is not idempotent and cannot recover when
Promotion is unavailable. The queue therefore uses at-least-once delivery, an
English versioned payload, an Order outbox, and consumer idempotency keyed by
`order_id`. This decision does not make Order call Promotion directly.

## Consequences

- Order can publish only after the authoritative Cart quote supplies a coupon
  code and Order persists that snapshot.
- Promotion owns its durable queue and acknowledges a message only after its
  database transaction commits.
- The existing REST consume endpoint remains available for diagnostics, but the
  RabbitMQ consumer is the target checkout integration.
