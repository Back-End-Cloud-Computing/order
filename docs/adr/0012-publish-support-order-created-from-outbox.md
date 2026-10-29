# ADR 0012 — Publish OrderCreated for Support through the existing outbox

Status: implemented in this change; end-to-end validation recorded separately.

## Decision

Persist an OrderCreated outbox row in the same SQL transaction as the Order, its idempotency result and the existing OrderConfirmed event. Reuse Order.OrderCreatedEventId as message_id and event_id, and publish to the durable direct exchange ganjj.orders with the exact routing key order.created. Preserve OrderConfirmed and its configurable routing key for Promotion.

HTTP notification remains a best-effort fallback after commit. HTTP and AMQP use the same serializer and stable event_id. Support accepts either first and acknowledges the duplicate without sending a second notification. The outbox retains unpublished messages across restarts and retries broker failures with publisher confirms and mandatory routing; creation never waits for RabbitMQ.

Missing customer_email continues to skip Support notification, matching the HTTP behavior. No phone is invented: customer_phone remains optional and omitted until Order integrates the Client phone contract. No schema migration is needed because the existing outbox stores event type and JSON payload.

## Compatibility and proof

OrderCreated contains customer_email and is intended for the Support queue only; OrderConfirmed remains free of this field. A durable direct exchange routes each event by its own key. Unknown event types are rejected rather than silently routed to Promotion.

The SQL Server test checks both outbox rows and the Support contract. The real RabbitMQ test verifies routing, persistent properties, message_id and correlation for both event types. The existing HTTP test checks the shared JSON serializer. The support contract is mirrored exactly in docs/contrato-api.md; no credential is committed.
