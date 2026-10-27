# Treat Product identifiers as opaque strings

Product owns `product_id` and publishes it as a string, so Order stores and exposes that identifier as an opaque string of up to 64 characters instead of coercing it to a numeric type. This preserves the cross-service contract and allows UUIDs or other Product-generated identifiers; the trade-off is a schema migration and a public response-type change in Order, which is acceptable because no legacy Order data must be migrated.
