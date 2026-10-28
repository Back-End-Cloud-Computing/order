# ADR 0010 - Publish OrderConfirmed for Promotion

Status: Accepted and implemented

Date: 2026-10-25

Order and Promotion use a durable RabbitMQ direct exchange for the
`OrderConfirmed` event. Although internal REST already satisfies the course
rubric, its coupon-consume endpoint is not idempotent and cannot recover when
Promotion is unavailable. The queue therefore uses at-least-once delivery, an
English versioned payload, an Order outbox, and consumer idempotency keyed by
`order_id`. This decision does not make Order call Promotion directly.

The first implementation publishes `coupon_code: null`. This is already a valid
no-op in the accepted consumer contract and lets the messaging path operate
without inventing a coupon source. Adding a coupon to the public Order request
remains forbidden: the current Cart response applies coupon discount only to an
order-level total, while Order persists the sum of item final prices. Accepting
that value now would create inconsistent totals.

## Consequences

- Order stores `OrderConfirmed` in its SQL outbox in the same transaction as
  the Order and idempotency result, then publishes with confirms.
- Delivery is at least once. Promotion must keep idempotency by `order_id`.
- A future authoritative Cart quote can populate `coupon_code` without changing
  the version 1 event contract.
- Promotion owns its durable queue and acknowledges a message only after its
  database transaction commits.
- The existing REST consume endpoint remains available for diagnostics, but the
  RabbitMQ consumer is the target checkout integration.
