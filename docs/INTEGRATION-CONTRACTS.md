# Integration Contracts - GANJJ Order Service

Status: Partially accepted - open items are tracked in `INTEGRATION-ISSUES.md`  
Last updated: 2026-08-29

## 1. Purpose

Define the minimum contracts that must exist before the Order service can be
integrated. These are service contracts, not shared database models or shared
language-specific libraries.

## 2. Shared HTTP conventions

- JSON encoded as UTF-8.
- Timestamps use ISO 8601 UTC.
- Money values use JSON numbers with at most two decimal places.
- Stable external IDs must preserve their type across services.
- `X-Correlation-ID` is forwarded by every service.
- Unsafe internal operations include a stable `operation_id`.
- Errors use a stable code plus safe detail; internal stack traces are omitted.
- Internal authentication initially uses `x-internal-secret`; a stronger mechanism
  may replace it without changing business payloads.

## 3. Authentication/JWT contract

### Accepted provider claims

The Authorization repository is now the provider source of truth. Claim names
and values are case-sensitive.

```json
{
  "iss": "ganjj-authorization",
  "sub": "d3f1a2b4-0000-4000-8000-000000000001",
  "email": "rodrigo@example.com",
  "role": "CLIENTE",
  "typ": "access",
  "iat": 1787280000,
  "exp": 1787280900
}
```

Administrator tokens use `role: ADMIN`.

### Initial validation rule

Order derives account identity only from UUID `sub`
and maps `role == ADMIN` to its Admin policy. It validates the token locally with
the Authorization public key, fixes the accepted algorithm to RS256, requires
issuer `ganjj-authorization`, checks lifetime, and requires `typ == access` plus
a non-empty `email` claim used by the Support notification contract.
Audience is not validated because the provider does not emit `aud`.

The public key is available from `GET /auth/public-key` and is supplied to Order
as a configured PEM file. Order never receives the private key and does not call
Authorization for each request. A real provider smoke token is still required
before final integration acceptance.

## 4. Cart checkout quote

Owner: Shopping Cart (João Liz)  
Status: Cart service implemented; checkout-quote capability not implemented

The current Cart stores a display snapshot of Product price in Redis. The target
operation must revalidate Product and Promotion data at checkout time and produce
the final commercial quote.

### Accepted interim contract

Until the group approves and implements checkout quotes, Order integrates with
the Cart API that exists today:

```http
GET /api/cesta
Authorization: Bearer <customer-access-token>
X-Correlation-ID: <id>
```

Current successful response:

```json
{
  "itens": [
    {
      "id": 12,
      "produto_id": 12,
      "nome": "Camiseta GANJJ",
      "preco": 50.00,
      "cor": "preta",
      "imagem_url": null,
      "quantidade": 2
    }
  ]
}
```

For this interim version, Order treats `preco` received server-to-server from Cart
as the item snapshot and calculates only `sum(preco * quantidade)`. Order does not
apply promotion rules. The access token is forwarded because the current Cart has
no internal read endpoint.

The current Cart has no quote/version/consume operation. Order therefore does not
automatically clear the cart after creating an order in the interim version; a
blind `DELETE /api/cesta` could remove products added concurrently. These are
documented limitations, not the final target contract.

### Target checkout-quote contract (pending group decision)

### Create quote

```http
POST /internal/carts/{customerId}/checkout-quotes
x-internal-secret: <secret>
X-Correlation-ID: <id>
Idempotency-Key: <order-checkout-key>
```

No price or product list is supplied by Order.

Successful response:

```json
{
  "quote_id": "2ee6757e-ab27-42df-bf60-738c848ff252",
  "customer_id": "d3f1a2b4-0000-4000-8000-000000000001",
  "cart_version": 8,
  "created_at": "2026-08-21T03:00:00Z",
  "expires_at": "2026-08-21T03:05:00Z",
  "items": [
    {
      "product_id": 12,
      "name": "Camiseta GANJJ",
      "size": "M",
      "image_url": null,
      "quantity": 2,
      "unit_list_price": 50.00,
      "unit_discount": 10.00,
      "unit_final_price": 40.00,
      "line_total": 80.00,
      "promotion_id": "PROMO-10",
      "promotion_name": "Desconto de inverno"
    }
  ],
  "subtotal": 100.00,
  "discount_total": 20.00,
  "total": 80.00,
  "currency": "BRL"
}
```

### Quote invariants

- `customer_id` must equal the authenticated/requested customer.
- Quote must contain at least one item.
- Quantities must be positive integers.
- `unit_final_price = unit_list_price - unit_discount`.
- `line_total = unit_final_price * quantity`.
- `total` equals the sum of line totals.
- Quote expiration is short and explicit.
- The same idempotency key returns the same unexpired quote.
- Quote is immutable after creation.

### Consume quote and clear matching cart version

```http
POST /internal/carts/{customerId}/checkout-quotes/{quoteId}/consume
x-internal-secret: <secret>
X-Correlation-ID: <id>
Idempotency-Key: <outbox-message-id>
Content-Type: application/json

{
  "cart_version": 8,
  "order_id": "adc8aacb-51a2-40d1-a8c0-e7ef5464f8aa"
}
```

Expected behavior:

- idempotent for the same quote/order;
- marks the quote consumed;
- clears the cart only if its current version is still 8;
- never deletes products added after quote creation;
- returns success when the same consumption is repeated.

### Cart error codes

| HTTP | Code | Meaning |
| --- | --- | --- |
| 404 | `cart_not_found` | Customer has no cart |
| 409 | `cart_empty` | Cart exists but has no items |
| 409 | `quote_expired` | Quote is no longer valid |
| 409 | `quote_consumed` | Quote already belongs to another completed checkout |
| 409 | `cart_version_changed` | Cart changed before quote/consume action |
| 422 | `quote_invalid` | Quote arithmetic or item data is invalid |
| 503 | `pricing_unavailable` | Product or Promotion cannot produce a quote |

### Required Cart work

1. Add cart versioning.
2. Add product size/variant if required by the catalog.
3. Consult Product and Promotion at checkout time.
4. Calculate subtotal, discounts, and total.
5. Store short-lived quote data in Redis.
6. Add create/consume internal endpoints.
7. Make both operations idempotent.
8. Add contract examples/tests shared with Order.

## 5. Product stock contract

Owner: Product (João Correa)  
Status: Must be agreed with the new FastAPI/MongoDB service

The original service offers per-product decrement and restore. The target keeps
that simple Saga shape but adds stable operation IDs.

### Decrement stock

```http
POST /internal/products/{productId}/stock/decrement
x-internal-secret: <secret>
X-Correlation-ID: <id>
Idempotency-Key: <stable-operation-id>
Content-Type: application/json

{
  "quantity": 2,
  "reason": "order_checkout",
  "reference_id": "<quote-id>"
}
```

Success:

```json
{
  "operation_id": "<stable-operation-id>",
  "product_id": 12,
  "remaining_stock": 18
}
```

### Restore stock

```http
POST /internal/products/{productId}/stock/restore
x-internal-secret: <secret>
X-Correlation-ID: <id>
Idempotency-Key: <stable-compensation-id>
Content-Type: application/json

{
  "quantity": 2,
  "reason": "order_checkout_compensation",
  "reference_id": "<quote-id>",
  "compensates_operation_id": "<decrement-operation-id>"
}
```

Both operations must return the original result when the same operation ID is
repeated. This is necessary before enabling HTTP retries for POST.

### Product error codes

| HTTP | Code | Meaning |
| --- | --- | --- |
| 404 | `product_not_found` | Product does not exist/is unavailable |
| 409 | `insufficient_stock` | Requested quantity cannot be decremented |
| 422 | `invalid_quantity` | Quantity is invalid |
| 409 | `operation_conflict` | Operation ID reused with a different request |
| 503 | `stock_unavailable` | Stock subsystem unavailable |

Insufficient-stock response may include `available_stock`.

## 6. Customer snapshot contract

The customer ID always comes from the validated JWT. Name and email snapshots
can follow one of two approaches:

1. include trusted `name` and `email` claims in the JWT; or
2. expose an internal Customer endpoint queried during checkout.

Recommended for the academic scope: include the values in the JWT, avoiding an
additional synchronous dependency. Order treats them as display snapshots, not
authorization inputs.

## 7. API Gateway contract

The gateway must:

- route `/api/pedidos` to Order;
- forward `Authorization`, `Cookie`, `Idempotency-Key`, and `X-Correlation-ID`;
- not inject customer IDs or prices into request bodies;
- preserve relevant status codes and `Location` headers;
- apply CORS/rate limiting without changing the Order contract.

Verified in the Gateway `routing.test.ts` contract test: both order route shapes
reach `order:8080`; the request headers and body cross unchanged; the response
status and `Location` are preserved; and a client-supplied internal secret is
removed. Order adds the received correlation ID to its structured logging scope.

## 8. Support notification

Owner: Support (Eduardo)

Status: Accepted and implemented over internal HTTP for N2

After the order transaction commits, Order calls:

```http
POST http://support:8090/internal/notifications
x-internal-secret: <shared-secret>
X-Correlation-ID: <correlation-id>
Content-Type: application/json
```

The body is the definitive `OrderCreated` payload:

```json
{
  "event_id": "e82a1f56-f69f-4dc4-9f10-3a05b3e90fac",
  "event_type": "OrderCreated",
  "occurred_at": "2026-08-21T03:00:01Z",
  "order_id": "adc8aacb-51a2-40d1-a8c0-e7ef5464f8aa",
  "order_code": "#GNJ-20260821-0001",
  "customer_id": "d3f1a2b4-0000-4000-8000-000000000001",
  "customer_email": "rodrigo@example.com",
  "total": 80.00,
  "currency": "BRL",
  "correlation_id": "checkout-correlation-id"
}
```

`event_id` is a UUID stored with the order and is reused if the event is ever
redelivered. `customer_email` comes from the JWT `email` claim. The optional
`customer_phone` is omitted until Client exposes a stable contract. Support
failures and the two-second timeout are logged after commit and never fail or
compensate the completed order. RabbitMQ can later transport this same payload.

## 9. RabbitMQ `OrderConfirmed` contract with Promotion

Status: Accepted in Order and Promotion; publisher and consumer implementation
remain separate follow-up work.

The shared names use English, matching the group's payload convention. The
earlier Portuguese proposal `pedido.confirmado` is replaced by:

| Element | Value |
| --- | --- |
| Exchange | `ganjj.orders` |
| Exchange type | `direct`, durable |
| Routing key | `order.confirmed` |
| Promotion queue | `promotion.order-confirmed`, durable |
| Binding | `promotion.order-confirmed` -> `ganjj.orders` with `order.confirmed` |
| Delivery | persistent message, publisher confirm, manual consumer ack |
| Content type | `application/json` |

Payload version 1:

```json
{
  "schema_version": 1,
  "event_id": "e82a1f56-f69f-4dc4-9f10-3a05b3e90fac",
  "event_type": "OrderConfirmed",
  "occurred_at": "2026-10-25T12:00:00Z",
  "order_id": "adc8aacb-51a2-40d1-a8c0-e7ef5464f8aa",
  "coupon_code": "INVERNO20",
  "items": [
    {
      "product_id": "6ad83139dbb96e49bc0aef29",
      "quantity": 2
    }
  ],
  "correlation_id": "6a2f41a5-c3f9-4d0f-a8cb-0a6e8a8e1a11"
}
```

Rules:

- `schema_version` is exactly `1`; incompatible changes require a new version.
- `event_id` is a stable UUID and becomes the AMQP `message_id`.
- `order_id` is a UUID and is Promotion's idempotency key.
- `coupon_code` is an uppercase string or `null`. A null coupon is a valid no-op
  that the consumer acknowledges without incrementing usage.
- `product_id` is a string to preserve the Product service identifier type;
  `quantity` is a positive integer.
- `correlation_id` is optional in the JSON and is also copied to the AMQP
  correlation property when present.
- Order writes the event to an outbox in the same SQL transaction as the order,
  then publishes with confirms. RabbitMQ delivery is at least once.
- Promotion records a unique processed `order_id` in the same MySQL transaction
  that increments coupon usage. A duplicate is acknowledged without increment.
- Promotion acknowledges only after commit; transient failures are negatively
  acknowledged for redelivery. Invalid version/payload is rejected and must be
  operationally visible instead of looping forever.
- No customer identity, e-mail, address, price, or other personal data is sent.

Implementation dependency: the current Order Cart adapter does not receive or
persist the coupon code. The publisher stays disabled until the authoritative
Cart checkout quote provides `coupon_code`; inventing or accepting the coupon
from the public Order request is forbidden.

## 10. Contract freeze checklist

- [ ] Cart agrees with quote route and payload.
- [ ] Promotion rules are represented in the quote snapshot.
- [ ] Cart version and consume semantics are agreed.
- [ ] Product agrees with decrement/restore paths and operation idempotency.
- [x] JWT issuer, algorithm, and claim names are implemented by Authorization.
- [x] Order accepts the Authorization contract through ADR 0006 and local consumer tests.
- [x] A real provider access token passes the cross-service smoke test and its refresh token is rejected.
- [x] Gateway header forwarding is verified.
- [x] Support HTTP payload, shared secret, correlation ID, and non-blocking failure behavior are covered by tests.
- [x] RabbitMQ `OrderConfirmed` names, payload, delivery, and idempotency contract match Promotion.
- [ ] Order publisher and Promotion consumer pass duplicate/redelivery tests.
- [ ] Success and each named error have JSON examples.
- [ ] Order consumer contract tests pass against provider examples.
- [ ] Real cross-service smoke checkout succeeds before final presentation.
