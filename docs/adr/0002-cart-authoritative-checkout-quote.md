# ADR 0002 - Cart produces the authoritative checkout quote

Status: Target accepted; persisted-coupon interim implemented
Date: 2026-08-22
Updated: 2026-11-03

## Context

Cart composes Product prices and Promotion discounts. Since 2026-11-03 it also
persists an accepted coupon and distributes its discount into final item prices,
but it still does not persist a checkout quote with expiration or cart version.
Accepting prices from an external client would allow stale or manipulated orders.
Reimplementing promotion rules in Order would duplicate domain ownership.

## Decision

The target remains a short-lived server-to-server checkout quote produced by
Cart, with Product and Promotion information composed outside Order.

Until that provider capability is approved and implemented, Order calls the
existing authenticated `GET /api/cart`, stores each returned `price` as list
price and `discounted_price` as final unit price, derives the unit discount, and
calculates the total. When Cart returns `coupon.applied: true`, Order also copies
`coupon.code` to the `OrderConfirmed` event. Order never accepts items, prices or
coupons from the external create-order body and does not implement promotion
rules.

## Consequences

- Cart needs new create/consume quote endpoints and versioning.
- Order depends synchronously on Cart for checkout but not for order reads.
- Promotion logic remains outside Order.
- The interim Cart snapshot includes product and coupon discounts but may become
  stale because it is not versioned; this limitation is tracked in
  `INTEGRATION-ISSUES.md`.
- Order does not clear the current unversioned Cart automatically.
- When the target quote exists, quote snapshots become Order's immutable
  commercial source without changing the external create-order payload.
