# ADR 0001 - Order owns SQL Server data and historical snapshots

Status: Accepted  
Date: 2026-08-21

## Context

The original service joins order rows with shared Customer and Product tables.
The new project must be independently deployable and use SQL Server while the
other domains use their own databases.

## Decision

Only Order may read or write its SQL Server schema. It stores external identifiers
without cross-service foreign keys and persists the customer, delivery, product,
price, and promotion values needed to reproduce an order historically.

## Consequences

- No cross-database joins.
- Existing orders survive later product/customer changes.
- Some data is deliberately duplicated.
- Real-time customer/product data requires a service call and is not part of the
  order-history read path.

