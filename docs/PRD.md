# PRD - GANJJ Order Service

Status: Draft for group review  
Owner: Rodrigo Alves  
Repository: `Back-End-Cloud-Computing/order`  
Last updated: 2026-09-16

## 1. Purpose

Rebuild the order capability from the original GANJJ API as an independently
deployable microservice implemented in C# with ASP.NET Core and backed only by
Microsoft SQL Server.

The academic goal is to demonstrate microservice and cloud-computing techniques.
Frontend development and migration of legacy data are not part of the project.

## 2. Context

The original Node.js order service shares one PostgreSQL database with customer,
product, cart, and dashboard functionality. It creates orders, reads customer and
product tables through SQL joins, and coordinates stock decrement through HTTP.
This coupling prevents independent ownership and deployment.

The new service is one of five domain microservices:

| Domain | Owner | Technology | Database |
| --- | --- | --- | --- |
| Customer | Eduardo Fabri | Java / Spring Boot | PostgreSQL |
| Product | João Correa | Python / FastAPI | MongoDB |
| Shopping cart | João Liz | Node.js / Express | Redis |
| Order | Rodrigo Alves | C# / ASP.NET Core | SQL Server |
| Promotion | Lucas Stopinski | PHP / Laravel | MySQL |

The group selected Eduardo's Support microservice for e-mail, SMS, and WhatsApp.
For N2, Order integrates with it through internal HTTP after checkout commits.

## 3. Product goal

Enable an authenticated customer to convert the current shopping cart into a
durable order while preserving the exact commercial snapshot and coordinating
stock changes without sharing databases.

## 4. Success criteria

The service is successful when the team can demonstrate that:

1. It runs independently from the original GANJJ repository.
2. It is the only service that reads or writes the order SQL Server database.
3. A customer can create and query an order through REST endpoints.
4. Item prices come server-to-server from Cart, never from client-supplied price
   fields. Promotion-aware checkout quotes remain a group-level enhancement.
5. Stock is decremented through the Product service and compensated on failure.
6. A repeated checkout request with the same idempotency key does not create a
   second order.
7. Application and SQL Server run as separate containers on the same network.
8. Configuration is externalized through environment variables.
9. The application image is explicitly versioned and can be published to a
   registry.
10. Automated tests demonstrate the critical happy paths and failure paths.

## 5. Users and actors

### Customer

- Creates an order from their own cart.
- Lists their own orders.
- Reads the details and tracking state of their own order.

### Administrator

- Lists all orders.
- Reads any order.
- Updates the tracking step of an order.

### Shopping Cart service

- Currently supplies item snapshots through `GET /api/cart`.
- Is the server-side source of product IDs, names, list/final prices, colors,
  images, and quantities
  used by the initial Order implementation.
- May later produce and consume a versioned, promotion-aware checkout quote after
  the group approves that contract.

### Product service

- Decrements stock for each quoted item.
- Restores previously decremented stock during compensation.

### Authentication/Customer capability

- Issues the JWT used by the Order service.
- Defines the stable customer identifier and administrator claim.

### Support microservice

- Accepts the definitive `OrderCreated` payload over internal HTTP for N2.
- Deduplicates notifications by the persisted event UUID.
- Its own implementation remains outside the Order repository.

## 6. Scope

### In scope

- Authenticated order creation from the current server-to-server Cart snapshot.
- Historical snapshots of customer, delivery, product, and available price data.
- SQL Server persistence owned exclusively by Order.
- Best-effort post-commit `OrderCreated` notification to Support.
- Stock coordination with compensating actions.
- Idempotent order creation.
- Customer order list and detail endpoints.
- Administrator list and tracking-step update endpoints.
- OpenAPI documentation and standardized error responses.
- Liveness and readiness health endpoints.
- Structured logs and correlation identifiers.
- Docker image, local multi-container execution, and registry-ready versioning.
- Local Kubernetes deployment with three Order replicas, internal Service DNS,
  persistent SQL Server storage, and Pod recovery demonstration.
- TDD workflow and automated tests.

### Out of scope

- Frontend work.
- Payment processing.
- Freight calculation or carrier integration.
- Returns, refunds, and exchanges.
- Editing the commercial contents of an order after creation.
- Migrating old PostgreSQL order data.
- Sharing SQL Server tables with another service.
- Implementing the Support service itself.
- AWS publication and production-grade cloud infrastructure.

## 7. Core user journeys

### Journey A - Successful checkout

1. The authenticated customer sends a create-order request with a delivery
   address and an idempotency key.
2. Order requests the authenticated customer's current items from Cart.
3. Cart returns Product data composed with Promotion discounts and Order validates
   identifiers, quantities, and price arithmetic.
4. Order decrements stock through Product.
5. Order saves the order and items in one SQL Server transaction.
6. Order returns `201 Created` with the durable order snapshot.
7. Order does not automatically clear the unversioned Cart in this interim flow.

### Journey B - Insufficient stock

1. Cart supplies a valid item snapshot.
2. Product rejects one item because stock is insufficient.
3. Order restores every item already decremented in the current attempt.
4. No order is persisted.
5. The API returns a domain error identifying unavailable stock.

### Journey C - Persistence failure after stock decrement

1. All stock decrements succeed.
2. The SQL Server transaction fails.
3. Order attempts compensation for every decremented item.
4. The failed checkout is logged with correlation and operation identifiers.
5. The API returns an error without pretending that an order was created.

### Journey D - Duplicate client request

1. A request is retried with an already processed `Idempotency-Key`.
2. Order returns the stored result of the original operation.
3. No new Cart read, stock decrement, or order insert occurs.

## 8. Functional requirements

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-001 | Authenticate requests using the group JWT contract. | Must |
| FR-002 | Derive customer identity from the validated token, not the request body. | Must |
| FR-003 | Create an order only from the authenticated server-to-server Cart snapshot. | Must |
| FR-004 | Reject an empty or malformed Cart snapshot. | Must |
| FR-005 | Never accept client-supplied product prices or discounts. | Must |
| FR-006 | Decrement stock through Product before confirming an order. | Must |
| FR-007 | Compensate prior stock decrements if a later decrement or persistence fails. | Must |
| FR-008 | Persist order and item snapshots atomically in SQL Server. | Must |
| FR-009 | Require an `Idempotency-Key` for order creation. | Must |
| FR-010 | Return the same outcome for repeated completed idempotent requests. | Must |
| FR-011 | Allow a customer to list only their own orders. | Must |
| FR-012 | Allow a customer to read only their own order; admins may read any order. | Must |
| FR-013 | Allow admins to list all orders. | Must |
| FR-014 | Allow admins to update tracking step from 0 through 5. | Must |
| FR-015 | Map tracking steps 0-2 to `pending`, 3-4 to `shipping`, and 5 to `completed`. | Must |
| FR-016 | Expose generated OpenAPI documentation through a Scalar interactive UI. | Must |
| FR-017 | Expose unauthenticated liveness and readiness endpoints. | Must |
| FR-018 | Return consistent RFC 9457-style problem responses for errors. | Should |
| FR-019 | Upgrade Cart integration to a versioned, promotion-aware checkout quote after group approval. | Could |
| FR-020 | After commit, send the persisted `OrderCreated` event to Support without failing the order when Support is unavailable. | Must |
| FR-021 | After the authoritative Cart quote supplies a coupon, eventually publish the accepted `OrderConfirmed` contract for idempotent consumption by Promotion. | Could |

## 9. Non-functional requirements

| ID | Requirement |
| --- | --- |
| NFR-001 | Order must not query Customer, Product, Cart, or Promotion databases. |
| NFR-002 | SQL Server credentials and service URLs must come from configuration, never source control. |
| NFR-003 | Logs must be structured and include correlation ID, order ID when known, and dependency name. |
| NFR-004 | Dependency calls must have explicit timeouts and no blind retries for unsafe POST operations. |
| NFR-005 | Database transient-failure handling must use SQL Server-aware retry behavior without duplicating the business operation. |
| NFR-006 | The API container must be stateless and support termination signals gracefully. |
| NFR-007 | The runtime image must use an explicit .NET 10 tag and an unprivileged runtime user. |
| NFR-008 | Docker image versions must be immutable release tags; deployment must not depend only on `latest`. |
| NFR-009 | Critical domain and application branches must be covered by automated tests. |
| NFR-010 | A local developer must be able to start API and database with one documented Compose command. |
| NFR-011 | Read endpoints should respond within 500 ms locally, excluding startup and external network latency. |
| NFR-012 | Checkout should either complete or return a controlled error within the configured dependency timeout budget. |

## 10. Business rules

1. Quantity must be a positive integer.
2. Money is stored as decimal values, never binary floating point.
3. In the interim flow, list price equals Cart's `price`, final price equals
   `discounted_price`, and the persisted total is
   `sum(discounted_price * quantity)`.
4. The Cart is loaded for the authenticated `id`; no customer ID comes from JSON.
5. An order item is an immutable commercial snapshot.
6. Removing or renaming a product later does not change an existing order.
7. Promotion changes later do not change an existing order.
8. A customer cannot read another customer's order.
9. Only an administrator may update tracking progress.
10. The current unversioned Cart is not automatically cleared after persistence.
11. A compensation failure is a high-severity operational condition and must not
    be silently ignored.

## 11. API compatibility position

The public route family remains `/api/pedidos` to reduce integration changes with
the original GANJJ contract. Internal C# types use English names and map to the
documented JSON contract.

The create-order payload intentionally changes: item and price fields are removed
from the client request because checkout data must be obtained from Cart.

## 12. Dependencies and assumptions

- The initial implementation uses the existing `GET /api/cart` contract and
  forwards the customer's Bearer token.
- Cart checkout quotes, cart versioning, safe consume, and promotion composition
  remain pending in `INTEGRATION-ISSUES.md`.
- Product will provide idempotent stock decrement and restore operations, or the
  team will accept the documented limitations of the original per-item Saga.
- JWT claim names and signing configuration will be agreed across all services.
- The API Gateway will forward authentication and correlation headers.
- No legacy data import is required.

## 13. Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Current Cart snapshot is not versioned and coupon is query-only | Order cannot safely consume a coupon or prove the cart did not change | Keep coupon out of Order until Cart provides a persisted checkout quote |
| Current Cart has no safe consume/version operation | Clearing may remove newly added products | Do not auto-clear in the interim implementation |
| Product operations are not idempotent | Retries may duplicate stock changes | Require operation IDs and disable blind POST retries |
| Compensation fails | Stock and order state diverge | Structured alert, persisted recovery record, manual/retry procedure |
| JWT claims differ among languages | Authentication breaks at integration | Freeze a shared token example and contract test |
| Local Kubernetes lacks memory or disk | Pods fail to schedule or SQL Server cannot start | Reserve cluster resources and verify the PVC before the presentation |
| Support is unavailable after commit | Notification can be lost in the N2 HTTP stage | Keep the order completed, log the event UUID, and migrate the same payload to RabbitMQ later |

## 14. Acceptance criteria

The PRD is accepted when the group agrees that:

- Order receives no product prices from the external client.
- Cart provides the server-to-server item/price snapshot for the interim version.
- Promotion-aware quote and safe Cart consumption remain explicit group issues.
- Product owns stock and exposes compensatable operations.
- SQL Server belongs only to Order.
- Public order behaviors and authorization rules are correct.
- Support notification uses internal HTTP now and preserves the payload for RabbitMQ later.
- Docker and TDD deliverables match the course expectations.

## 15. Open group decisions

These do not block documentation or the initial Order domain implementation:

1. Final local Kubernetes namespace and Gateway/Ingress routing shared by the group.
2. Whether Docker Hub publication is additionally required for the final presentation.
3. The final Product stock-reservation contract.
