# SDD - GANJJ Order Service

Status: Draft for technical review  
Target runtime: .NET 10 / ASP.NET Core  
Database: Microsoft SQL Server  
Last updated: 2026-09-16

## 1. Architecture objectives

The design prioritizes the course evaluation criteria and a clear demonstration
of cloud-native practices:

- one independently deployable process;
- database per service;
- REST communication with explicit contracts;
- containerized application and database;
- externalized configuration;
- reproducible and versioned images;
- failure handling across service boundaries;
- automated tests written with TDD;
- a reproducible local Kubernetes deployment without AWS publication.

The design intentionally avoids production-scale complexity that does not improve
the academic demonstration.

## 2. Architecture principles

1. **Order owns order data.** No other service reads the SQL Server schema.
2. **Snapshots over cross-database joins.** Order stores the historical customer,
   delivery, product, and available price values required to display an order.
3. **Cart is the server-side price source.** The interim implementation consumes
   Cart's existing item snapshots. Order never accepts client prices and never
   implements promotion rules; a promotion-aware quote remains the target.
4. **Product owns stock.** Order coordinates stock changes through the Product API.
5. **Failures are explicit.** A partial checkout triggers compensation and is
   observable; errors are not swallowed.
6. **Idempotency precedes retries.** Unsafe operations are retried only when their
   contract supplies an operation identifier.
7. **The API container is stateless.** Persistent state belongs to SQL Server.
8. **Configuration is environment-specific.** The same image can run under
   Compose or local Kubernetes.

## 3. Technology choices

| Concern | Choice | Reason |
| --- | --- | --- |
| Runtime | .NET 10 LTS | Current LTS installed in the development environment |
| HTTP API | ASP.NET Core Controllers | Clear validation, authorization, and OpenAPI behavior |
| Persistence | Entity Framework Core with SQL Server provider | Migrations, transactions, and provider support |
| Database | SQL Server 2022 in a local persistent container | Stable course/demo baseline |
| Authentication | ASP.NET Core JWT Bearer with RS256, `sub`, `role`, and `typ` | Matches the implemented Authorization service contract in ADR 0006 |
| API description | Built-in ASP.NET Core OpenAPI + Scalar | Generated contract plus interactive API visualization |
| Errors | Problem Details | Consistent machine-readable API errors |
| HTTP dependencies | Typed `HttpClient` clients | Dependency injection, timeout, and resilience policies |
| DTO mapping | Manual mappings and EF Core `Select` projections | Keeps domain/snapshot decisions explicit and avoids an unnecessary mapper dependency |
| Tests | xUnit test projects and container-backed integration tests | Supports Red-Green-Refactor and real SQL semantics |
| Packaging | Multi-stage Docker image | Small runtime image and reproducible build |
| Local orchestration | Docker Compose and Minikube | Compose supports development; Kubernetes demonstrates Deployments, Services, DNS, and recovery |

Exact NuGet patch versions will be pinned when implementation starts. The design
does not depend on preview packages.

## 4. System context

```mermaid
flowchart LR
    Client[API client] --> Gateway[API Gateway]
    Gateway --> Order[Order Service<br/>ASP.NET Core]
    Gateway --> Customer[Customer/Auth Service]
    Order --> Cart[Shopping Cart Service<br/>Redis]
    Cart --> Product[Product Service<br/>MongoDB]
    Cart --> Promotion[Promotion Service<br/>MySQL]
    Order --> Product
    Order -. future OrderConfirmed .-> Promotion
    Order --> Sql[(Order SQL Server)]
    Order -->|post-commit internal HTTP| Support[Support Microservice]
```

Only Order connects to the Order SQL Server database. Arrows between services are
HTTP for N2. Support may later consume the same `OrderCreated` payload through
RabbitMQ. Promotion has a separate accepted `OrderConfirmed` RabbitMQ contract
whose publisher and consumer are not implemented yet.

## 5. Repository and solution structure

Planned structure after the implementation phase is authorized:

```text
order/
  src/
    Ganjj.Order.Api/
    Ganjj.Order.Application/
    Ganjj.Order.Domain/
    Ganjj.Order.Infrastructure/
  tests/
    Ganjj.Order.Domain.Tests/
    Ganjj.Order.Application.Tests/
    Ganjj.Order.Api.IntegrationTests/
    Ganjj.Order.ContractTests/
  docs/
  Dockerfile
  docker-compose.yml
  .dockerignore
  Ganjj.Order.sln
```

### `Ganjj.Order.Domain`

- `Order` aggregate and `OrderItem` entity.
- Money and delivery-address value objects.
- Status/tracking-step rules.
- Domain invariants and domain exceptions.
- No references to ASP.NET Core, EF Core, or HTTP clients.

### `Ganjj.Order.Application`

- Use cases: create, get, list mine, list all, update tracking.
- Ports/interfaces for persistence, Cart, Product, Support notification, clock,
  and identifier creation.
- DTOs and use-case results.
- Saga orchestration and compensation decisions.
- No direct dependency on concrete infrastructure.

### `Ganjj.Order.Infrastructure`

- EF Core `OrderDbContext`, configurations, and migrations.
- SQL Server repositories and idempotency store.
- Typed Cart, Product, and Support HTTP clients.
- Extension point for a future outbox dispatcher.
- Configuration binding and dependency health checks.

### `Ganjj.Order.Api`

- Controllers and request/response models.
- Authentication and authorization policies.
- Problem Details mapping.
- OpenAPI, Scalar UI, health endpoints, correlation ID, and middleware composition.
- No business rule implementation inside controllers.

The split is intentionally limited to four production projects. Additional
framework abstractions or mediator libraries are unnecessary for this scope.

## 6. Public HTTP API

The authoritative proposed contract is `openapi/order-service.v1.yaml`.

ASP.NET Core generates the runtime OpenAPI document. Scalar renders that document
as the interactive API reference. The planned default routes are
`/openapi/v1.json` and `/scalar/v1`; exposure outside development/demo
environments remains configuration-controlled.

Request, application, domain, persistence, and response models are mapped
manually. Read-only EF Core queries should project directly to response DTOs with
`Select`. Mapster is not part of the initial dependency set.

| Method | Route | Authorization | Purpose |
| --- | --- | --- | --- |
| POST | `/api/pedidos` | Customer | Create order from current Cart snapshot |
| GET | `/api/pedidos/meus` | Customer | List authenticated customer's orders |
| GET | `/api/pedidos/{id}` | Owner or admin | Get one order snapshot |
| GET | `/api/pedidos/admin/todos` | Admin | List all orders |
| PUT | `/api/pedidos/{id}/passo` | Admin | Update tracking step |
| GET | `/health` | Anonymous | Basic service health |
| GET | `/health/live` | Anonymous | Process liveness |
| GET | `/health/ready` | Anonymous | SQL Server readiness |
| GET | `/instance` | Anonymous | Identify the Pod serving a request |

The Portuguese public routes preserve the original GANJJ integration shape.
Internal type and member names remain English.

## 7. Checkout sequence

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant O as Order API
    participant DB as SQL Server
    participant C as Cart
    participant P as Product

    Client->>O: POST /api/pedidos + JWT + Idempotency-Key
    O->>DB: Acquire idempotency key
    O->>C: GET /api/cesta with forwarded customer JWT
    C-->>O: current item and price snapshots
    O->>O: Validate items and calculate sum(preco * quantidade)
    loop each cart item
        O->>P: Decrement stock (operationId)
        P-->>O: success or domain error
    end
    O->>DB: Transaction: Order + Items + completed idempotency result
    DB-->>O: commit
    O-->>Client: 201 Created
```

If Product rejects an item or the database transaction fails, Order calls the
restore endpoint for every item already decremented in reverse order.

## 8. Create-order algorithm

1. Validate JWT, delivery address, and `Idempotency-Key`.
2. Calculate a stable hash of the relevant request body and authenticated user.
3. Insert an `InProgress` idempotency record under a unique key.
4. If the key already exists:
   - return the stored success for the same completed request;
   - return conflict for an in-progress request;
   - reject reuse of the key with a different request hash.
5. Request `GET /api/cesta` from Cart with the customer's forwarded Bearer token.
6. Validate item count, product identifiers, quantities, prices, and arithmetic.
   For the interim contract, set list/final unit price to Cart's `preco`, discount
   to zero, and calculate `sum(preco * quantidade)`.
7. For each item, request stock decrement with a stable operation ID.
8. In one SQL transaction:
   - insert Order;
   - insert OrderItems;
   - mark the idempotency record completed with the response snapshot.
9. After commit, attempt `OrderCreated` delivery to Support with a persisted event
   UUID and a short timeout. Log and continue if Support fails.
10. Return `201 Created`.

The interim implementation does not clear Cart. A future versioned quote/consume
contract can add a post-commit outbox action without changing the public request.

On a failure before SQL commit:

- restore every confirmed stock decrement;
- remove the idempotency record only after confirmed compensation so a safe retry
  can reuse the key;
- retain a failed record and emit a high-severity log if compensation is not
  confirmed.

## 9. Domain model

### Order aggregate

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | Public durable identifier |
| `Code` | string | Human-readable `#GNJ-...` code, unique |
| `CustomerId` | `Guid` | Authorization account UUID from `sub`, no database FK |
| `CustomerName` | string? | Optional snapshot when supplied by trusted token/service |
| `CustomerEmail` | string? | Optional snapshot for display/notification |
| `OrderCreatedEventId` | `Guid` | Unique, persisted Support idempotency key |
| `QuoteId` | `Guid?` | Reserved for the future Cart quote contract |
| `CartVersion` | long? | Reserved for future safe Cart consumption |
| `Status` | enum/string | `pending`, `shipping`, `completed`, `cancelled`, `failed` |
| `TrackingStep` | integer | Inclusive range 0-5 |
| `Total` | decimal(18,2) | Sum of immutable line totals |
| `DeliveryAddress` | value object | Historical address snapshot |
| `TrackingNumber` | string? | Optional carrier reference |
| `CreatedAt` | UTC timestamp | Server-controlled |
| `UpdatedAt` | UTC timestamp | Server-controlled |
| `Version` | rowversion | Optimistic concurrency token |

### Order item snapshot

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | Internal item identifier |
| `OrderId` | `Guid` | FK inside Order database only |
| `ProductId` | long | External Product identifier, no cross-service FK |
| `ProductName` | string | Immutable snapshot |
| `Size` | string? | Immutable variant snapshot |
| `ImageUrl` | string? | Optional display snapshot |
| `Quantity` | integer | Positive |
| `UnitListPrice` | decimal(18,2) | Interim: Cart `preco`; target: price before discount |
| `UnitDiscount` | decimal(18,2) | Interim: zero; target: discount per unit |
| `UnitFinalPrice` | decimal(18,2) | Interim: Cart `preco`; target: final price per unit |
| `LineTotal` | decimal(18,2) | `UnitFinalPrice * Quantity` |
| `PromotionId` | string? | Optional external promotion reference |
| `PromotionName` | string? | Historical promotion description |

### State mapping

| Tracking step | Status |
| --- | --- |
| 0, 1, 2 | `pending` |
| 3, 4 | `shipping` |
| 5 | `completed` |

An admin may move the step forward or backward to preserve the original behavior.
This can be tightened later without schema change.

## 10. Persistence design

```mermaid
erDiagram
    Orders ||--|{ OrderItems : contains

    Orders {
        uniqueidentifier Id PK
        nvarchar Code UK
        uniqueidentifier CustomerId
        uniqueidentifier OrderCreatedEventId UK
        uniqueidentifier QuoteId UK
        bigint CartVersion
        nvarchar Status
        int TrackingStep
        decimal Total
        nvarchar DeliveryAddressJson
        datetimeoffset CreatedAt
        datetimeoffset UpdatedAt
        rowversion Version
    }

    OrderItems {
        uniqueidentifier Id PK
        uniqueidentifier OrderId FK
        bigint ProductId
        nvarchar ProductName
        int Quantity
        decimal UnitListPrice
        decimal UnitDiscount
        decimal UnitFinalPrice
        decimal LineTotal
    }

    IdempotencyRequests {
        nvarchar Key PK
        varbinary RequestHash
        nvarchar Status
        int ResponseStatus
        nvarchar ResponseBody
        datetimeoffset CreatedAt
        datetimeoffset ExpiresAt
    }

```

### Constraints and indexes

- Unique `Orders.Code`.
- Filtered unique `Orders.QuoteId` when a future non-null quote ID is present.
- Index `Orders(CustomerId, CreatedAt DESC)` for customer history.
- Index `Orders(CreatedAt DESC)` for administrator listing.
- Check `TrackingStep BETWEEN 0 AND 5`.
- Check monetary values and quantities are non-negative/positive as applicable.
- Unique `IdempotencyRequests.Key`.
- If the future outbox is introduced, index pending messages by processing and
  occurrence timestamps.

Delivery address may be stored as owned columns or JSON. The implementation should
prefer owned columns if query/reporting is required; otherwise JSON is acceptable
for the immutable snapshot. The initial migration must make this choice explicit.

## 11. Transactions and consistency

### Strong consistency boundary

One SQL transaction covers:

- Order insert;
- all OrderItem inserts;
- idempotency completion;
- a future outbox insert only when a real post-commit integration action exists.

### Eventual consistency boundary

Stock and cart live outside SQL Server. They cannot participate in the local SQL
transaction. The service therefore uses an orchestrated Saga:

- Product stock decrement is the forward action.
- Product stock restore is the compensating action.
- The current Cart is read but not cleared. Future safe quote consumption becomes
  a post-commit outbox action.

No distributed database transaction or shared schema is introduced.

## 12. Integration clients and resilience

### Cart client

- Typed client with base URL from configuration.
- `GET /api/cesta` timeout budget: 3 seconds by default.
- Forward the authenticated customer's Bearer token; never log it.
- Validate and map the current Portuguese Cart payload at this anti-corruption
  boundary while keeping internal C# names in English.
- Do not call `DELETE /api/cesta` automatically without versioning.

### Product client

- Typed client with base URL from configuration.
- Each decrement and restore includes a stable operation ID.
- Timeout budget: 3 seconds per call by default.
- Retry only when Product confirms operation-level idempotency.
- Restore failures are logged as critical with all recovery identifiers.

### Support client

- `POST /internal/notifications` at the configured Support base URL.
- Send `x-internal-secret`, `X-Correlation-ID`, and the definitive
  `OrderCreated` JSON contract.
- Timeout budget: 2 seconds by default.
- Run only after SQL commit. Any HTTP error or timeout is logged and does not
  fail the order or trigger stock compensation.
- Omit `customer_phone` until the Client service exposes a stable lookup contract.

### Promotion event publisher (contract accepted, implementation pending)

- Durable direct exchange `ganjj.orders`, routing key `order.confirmed`, and
  Promotion-owned queue `promotion.order-confirmed`.
- Versioned English JSON contract in `INTEGRATION-CONTRACTS.md` section 9.
- Publish from a SQL outbox with confirms for at-least-once delivery.
- Do not enable until the authoritative Cart quote supplies a coupon code that
  Order persists. Never accept price or coupon data from the public Order body.

### Failure matrix

| Failure | API outcome | Required action |
| --- | --- | --- |
| Cart unavailable/timeout | 503 | No stock or order change |
| Cart empty | 409 | No stock or order change |
| Cart payload invalid | 502/422 | No stock or order change; report contract mismatch |
| Product not found | 409/422 | Compensate prior items |
| Insufficient stock | 409 | Compensate prior items |
| SQL transaction fails | 503/500 | Compensate all decremented items |
| Compensation fails | 500 + critical operational state | Preserve failed idempotency/recovery data |
| Duplicate idempotency key | Original result or 409 | Never repeat side effects blindly |
| Support unavailable/timeout/error | 201 after completed checkout | Log the failed event UUID; no compensation |

## 13. Authentication and authorization

- Accept `Authorization: Bearer <token>` as the primary mechanism.
- Validate signature locally with the Authorization public RSA key and accept
  only the RS256 algorithm.
- Require issuer `ganjj-authorization`, expiration, and `typ == access`.
- Require a non-empty `email` claim so every created order can produce the
  mandatory Support notification payload.
- Read the stable account identifier from the case-sensitive `sub` claim and
  parse it as a non-empty UUID.
- Map `role == ADMIN` to the `Admin` authorization policy.
- Do not require audience while Authorization does not emit `aud`.
- The create request never accepts `customerId` or `isAdmin` from JSON.
- Detail queries enforce owner-or-admin authorization after loading the order.
- Health endpoints are anonymous and disclose no credentials.
- Internal service credentials are read from secret-backed configuration.

This change is accepted in ADR 0006. The public key is supplied as a
mounted/configured PEM file; Order does not call
Authorization during ordinary request validation.

## 14. Error contract

Errors use `application/problem+json` and include:

- `type` - stable problem URI or identifier;
- `title` - concise category;
- `status` - HTTP status;
- `detail` - safe human-readable explanation;
- `instance` - request path;
- `traceId` - correlation identifier;
- optional domain extensions such as `productId` and `availableStock`.

Internal exception types are never returned to clients.

## 15. Observability

### Logs

- Structured logs to stdout/stderr for container collection.
- Correlation ID accepted from `X-Correlation-ID` or generated at entry.
- Log dependency, duration, result category, order ID, optional quote ID, and
  operation ID.
- Do not log JWTs, connection strings, passwords, or full personal addresses.

### Metrics/traces

The base design leaves an OpenTelemetry-compatible extension point for:

- checkout duration;
- order creation success/failure counts;
- dependency latency and failure counts;
- compensation attempt/failure counts;
- pending outbox message count.

The current API exposes Prometheus text metrics at `/metrics`, including request
count, responses in progress, status totals, and request-duration histograms.
The Order Pod carries Prometheus scrape annotations for port 8080. The endpoint
has no Order Ingress and is not routed by the API Gateway.

Console/CloudWatch logging is sufficient for the first course milestone;
OpenTelemetry export is a later cloud milestone.

### Health endpoints

- `/health`: stable basic health path shared by the GANJJ services.
- `/health/live`: process can respond; no dependency checks.
- `/health/ready`: verifies SQL Server connectivity with a lightweight check.
- External Product/Cart outages should not make every API replica unready unless
  the final orchestrator policy explicitly requires it.

## 16. Docker design

### Image build

The Dockerfile is multi-stage:

1. `mcr.microsoft.com/dotnet/sdk:10.0` restores and publishes the API artifact;
   tests run separately through `dotnet test`.
2. `mcr.microsoft.com/dotnet/aspnet:10.0` contains only runtime artifacts.
3. Runtime listens on container port `8080`.
4. Runtime uses a non-root user.
5. CI publishes the image to GHCR for `linux/amd64` and `linux/arm64`.

The pipeline publishes a moving demonstration tag and an immutable commit tag:

```text
ghcr.io/back-end-cloud-computing/order:latest
ghcr.io/back-end-cloud-computing/order:sha-<commit>
```

The Kubernetes manifest pulls `latest` for the classroom demonstration. The
bootstrap script can explicitly build and select `ganjj-order:local` with
`-UseLocalImage` while developing unpublished changes.

### Local containers

```mermaid
flowchart LR
    Host[Host localhost:3004] -->|3004:8080| Api[order-api :8080]
    Api -->|Server=sqlserver,1433| Db[(sqlserver :1433)]
```

Compose will define:

- `order-api` built from this repository;
- `sqlserver` using `mcr.microsoft.com/mssql/server:2022-latest`;
- a named SQL Server data volume;
- the default network created by Compose for this project;
- database health check and API dependency condition;
- environment variables without committed secret values.

Inside the API container, the database hostname is `sqlserver`, never `localhost`.

## 17. Local Kubernetes deployment

The current runtime target is Minikube on Docker Desktop. No AWS resources are
created. `scripts/Start-Kubernetes.ps1` configures the JWT public key and local
Secret, then installs or upgrades the `helm/order` chart. The chart exposes
image, replica count, resources, peer URLs, Gateway host metadata and optional
local SQL Server configuration as values; credentials remain in an external
Secret. By default Minikube pulls the public multi-architecture image from GHCR;
`-UseLocalImage` is the explicit fallback for unpublished development changes.

The Order Deployment declares three stateless replicas behind the `order`
ClusterIP Service on port 8080. SQL Server runs as a single StatefulSet Pod with
an 8 GiB persistent claim and the `order-sqlserver` Service on port 1433. Both
are in the `default` namespace so existing group services can use DNS names.
The Order Pod uses `shopping-cart:3003`, `product-api:8000`, and `support:8090` as peer addresses;
these names and ports match the Services published by the current group
repositories.

Liveness (`/health/live`) checks the API process; readiness (`/health/ready`)
checks SQL connectivity. SQL Server's readiness probe also runs `SELECT 1`
instead of only checking an open TCP port. A startup probe allows migrations to
finish before liveness checks begin. The `/instance` endpoint identifies the responding Pod
for the course replica demonstration. Local browser access uses `kubectl
port-forward service/order 3004:8080`. The Order chart creates no Ingress:
`api.ganjj.local` is declared only as the expected Gateway host, and the Gateway
remains the sole external entry point.

## 18. Database migrations and startup

- EF Core migrations are versioned in source control.
- Local Kubernetes sets `Database__ApplyMigrations=true`; EF Core 10's migration
  lock serializes concurrent replicas. This is a course-demo tradeoff, not a
  production deployment policy.
- A future production deployment should apply reviewed migrations in a
  one-shot task before updating API replicas, with separate schema permissions.
- Seed data is limited to optional demonstration fixtures; no old-data migration
  exists.

## 19. Configuration inventory

Proposed environment variable names:

| Variable | Required | Purpose |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Yes | Runtime environment |
| `ASPNETCORE_HTTP_PORTS` | No | Container port, default 8080 |
| `ConnectionStrings__OrderDatabase` | Yes | SQL Server connection |
| `Authentication__Jwt__Issuer` | Yes | Expected issuer, `ganjj-authorization` |
| `Authentication__Jwt__PublicKeyPath` | Yes | Mounted Authorization public-key PEM file |
| `Services__Cart__BaseUrl` | Yes | Cart URL |
| `Services__Product__BaseUrl` | Yes | Product URL |
| `Services__Support__BaseUrl` | Yes | Support URL |
| `Services__Internal__Secret` | Yes initially | Internal HTTP authentication |
| `Resilience__HttpTimeoutSeconds` | No | Dependency timeout budget |
| `Resilience__SupportTimeoutSeconds` | No | Post-commit Support timeout, default 2 seconds |
| `Outbox__PollIntervalSeconds` | No/future | Outbox dispatch interval when enabled |

Secrets are represented in `.env.example` only by placeholders.

## 20. CI quality gates

Planned GitHub Actions pipeline:

1. restore with locked dependency metadata;
2. build in Release mode with warnings treated according to project policy;
3. run unit tests;
4. run integration and contract tests;
5. publish test results/coverage;
6. build the Docker image;
7. smoke-test the built image and health endpoint;
8. on an approved version tag, publish an explicitly versioned image.

No AWS deployment stage or credentials are needed for the local course demo.

## 21. Architecture decisions

Detailed decisions and their consequences are recorded under `docs/adr`:

- database ownership and snapshots;
- interim Cart snapshot and target checkout quote;
- Saga, idempotency, and outbox behavior;
- container/registry portability;
- post-commit Support notification over internal HTTP (ADR 0009).
- RabbitMQ `OrderConfirmed` contract with Promotion (ADR 0010).
- adopted Authorization RS256 contract (ADR 0006).
- local Kubernetes deployment and no AWS publication (ADR 0007).

## 22. Technical acceptance checklist

- [ ] No SQL query references external-service tables.
- [ ] Create-order request contains no customer ID, price, or discount.
- [ ] SQL transaction covers order, items, and idempotency completion.
- [ ] Stock compensation tests cover partial and database failures.
- [ ] Owner/admin authorization tests pass.
- [ ] Only RS256 access tokens with the expected issuer and a valid UUID `sub` authenticate.
- [ ] Refresh tokens and non-admin roles are rejected by the corresponding policies.
- [ ] OpenAPI contract matches runtime behavior.
- [ ] Scalar renders the generated OpenAPI document in the demo environment.
- [ ] API and SQL Server run in separate containers.
- [ ] API connects using the database service name on the Docker network.
- [ ] Image uses a versioned tag and can be pulled on another machine.
- [ ] Health checks work inside the built container.
- [ ] Minikube has three ready Order Pods and one ready SQL Server Pod.
- [ ] The Order Service resolves by DNS from another Pod.
- [ ] A deleted Order Pod is replaced while the Service remains stable.
- [ ] Group-level pending decisions remain visible rather than silently assumed.
