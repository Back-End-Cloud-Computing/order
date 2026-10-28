# Ordering Context

The Ordering context creates and preserves the historical record of a customer's checkout while coordinating stock with external services.

## Language

**Order**:
A durable purchase record created from the authenticated customer's Cart snapshot.
_Avoid_: Checkout, transaction

**Order Item**:
An immutable commercial snapshot of one Product in an Order, including its external identifier, presentation data, quantity, and prices at checkout.
_Avoid_: Cart item, product row

**Product ID**:
An opaque textual identifier owned by Product and copied by Order without parsing or generating it.
_Avoid_: Product number, numeric product ID

**Cart Snapshot**:
The server-to-server item and pricing data returned by Cart for the authenticated customer at checkout time.
_Avoid_: Client cart, request items

**Stock Operation**:
An idempotent Product command identified by `Idempotency-Key` that either decrements stock or restores a previous decrement.
_Avoid_: Reservation

**Order Confirmed**:
A versioned integration fact emitted after an Order is durably created. It identifies the Order and its Product quantities; its coupon code can be absent.
_Avoid_: Order request, promotion command
