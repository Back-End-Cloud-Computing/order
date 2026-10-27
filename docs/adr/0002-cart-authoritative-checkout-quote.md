# ADR 0002 - Cart produces the authoritative checkout quote

Status: Target accepted; interim current-Cart contract accepted  
Date: 2026-08-22

## Context

The current Cart composes Product prices and Promotion discounts, but it does not
persist a checkout quote with expiration or cart version. Accepting prices from
an external client would allow stale or manipulated orders. Reimplementing
promotion rules in Order would duplicate domain ownership.

## Decision

The target remains a short-lived server-to-server checkout quote produced by
Cart, with Product and Promotion information composed outside Order.

Until that provider capability is approved and implemented, Order calls the
existing authenticated `GET /api/cart`, stores each returned `price` as list
price and `discounted_price` as final unit price, derives the unit discount, and
calculates the total. Order never accepts items or prices from the external
create-order body and does not implement promotion rules. Coupon data is deferred
until Cart provides a persisted authoritative quote rather than a query-only
calculation.

## Consequences

- Cart needs new create/consume quote endpoints and versioning.
- Order depends synchronously on Cart for checkout but not for order reads.
- Promotion logic remains outside Order.
- The interim Cart snapshot includes item promotion discounts but may become stale
  and cannot provide an authoritative coupon for Order; this limitation is tracked
  in `INTEGRATION-ISSUES.md`.
- Order does not clear the current unversioned Cart automatically.
- When the target quote exists, quote snapshots become Order's immutable
  commercial source without changing the external create-order payload.
