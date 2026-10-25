# ADR 0009 - Notify Support with post-commit internal HTTP

Status: Accepted

Date: 2026-10-25

Order sends the definitive `OrderCreated` payload to Support through internal
HTTP after the order transaction commits. The event UUID is stored with the
order, the e-mail comes from the authenticated JWT, and Support failures are
logged without failing the completed checkout or compensating stock. The phone
is omitted until a stable Client contract exists. This supersedes ADR 0005;
RabbitMQ may later carry the same payload, while the HTTP endpoint remains useful
for tests and diagnostics.

## Consequences

- The N2 flow uses `POST http://support:8090/internal/notifications` with the
  shared `x-internal-secret` and a two-second timeout.
- There is no delivery retry in this HTTP stage. A notification can be lost while
  Support is unavailable; the group accepted that limitation until RabbitMQ.
- Repeated delivery of the persisted event UUID remains safe because Support
  deduplicates `event_id`.
