# ADR 0002 - Cart produces the authoritative checkout quote

Status: Target accepted; interim current-Cart contract accepted  
Date: 2026-08-22

## Context

The current Cart stores a base-price snapshot but does not calculate promotions,
totals, quote expiration, or cart version. Accepting prices from an external
client would allow stale or manipulated orders. Reimplementing promotion rules in
Order would duplicate domain ownership.

## Decision

The target remains a short-lived server-to-server checkout quote produced by
Cart, with Product and Promotion information composed outside Order.

Until that provider capability is approved and implemented, Order calls the
existing authenticated `GET /api/cesta`, treats each returned `preco` as both
list and final unit price, applies zero discount, and calculates the total. Order
never accepts items or prices from the external create-order body and does not
implement promotion rules.

## Consequences

- Cart needs new create/consume quote endpoints and versioning.
- Order depends synchronously on Cart for checkout but not for order reads.
- Promotion logic remains outside Order.
- The interim Cart snapshot may be stale and has no promotion-aware final total;
  this limitation is tracked in `INTEGRATION-ISSUES.md`.
- Order does not clear the current unversioned Cart automatically.
- When the target quote exists, quote snapshots become Order's immutable
  commercial source without changing the external create-order payload.
