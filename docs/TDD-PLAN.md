# TDD Plan - GANJJ Order Service

Status: Draft for review  
Method: Test-Driven Development (Red-Green-Refactor)  
Last updated: 2026-08-29

## 1. Purpose

TDD is an implementation method, not a substitute for the PRD or SDD. This plan
defines the order in which executable tests will drive the code after the planning
documents are approved.

No production behavior should be implemented without first introducing a failing
test that describes the next observable rule.

## 2. TDD loop

Every behavior follows the same cycle:

1. **Red** - add the smallest test that expresses one missing behavior and confirm
   it fails for the intended reason.
2. **Green** - write the minimum production code required to pass that test.
3. **Refactor** - improve names, duplication, boundaries, and design while all tests
   remain green.
4. Commit a small coherent change only after the relevant suite passes.

A test that passes before production code changes is not evidence of a new TDD
cycle; it must be corrected or replaced by a test that demonstrates the gap.

## 3. Test layers

All .NET test projects use xUnit. The strategy is a backend test pyramid; a
frontend is not required at any layer:

1. **Base - unit tests:** Domain and Application behavior, fast and isolated.
2. **Middle - component/integration/contract tests:** the Order API, real SQL
   Server, authentication, serialization, and HTTP stubs that represent Cart and
   Product. These tests do not require the other repositories to be running.
3. **Top - cross-service end-to-end tests:** a small number of HTTP scenarios with
   the real Gateway, Auth, Cart, Product, Promotion, and Order containers. These
   tests require the other services, but still do not require a frontend.

Provider examples and deterministic stubs allow the middle of the pyramid to be
developed before Cart and Product are ready. Real provider services are required
only to validate the agreed contracts and run the small end-to-end suite.

### Domain unit tests

Fast, framework-independent tests for:

- money arithmetic;
- order-item validation;
- order total calculation;
- tracking-step/status mapping;
- immutable snapshot behavior;
- invalid state transitions and invariants.

These tests must not use a database, HTTP server, clock, or container.

### Application unit tests

Use-case tests with fake/mock ports for:

- current Cart snapshot client;
- Product stock client;
- Order repository/unit of work;
- idempotency store;
- outbox writer;
- clock and ID generator.

They verify orchestration order, compensation, authorization decisions, and the
absence of duplicated side effects.

### API integration tests

Run the ASP.NET Core application in-process and use a real SQL Server test
container. They verify:

- routing and model validation;
- JWT authorization policies;
- EF Core mappings, constraints, migrations, and transactions;
- Problem Details responses;
- serialization and OpenAPI-visible behavior.
- generated OpenAPI document and Scalar UI routes.

The EF Core in-memory provider is not accepted as evidence for SQL Server
behavior.

### Consumer contract tests

Order is the consumer of Cart and Product. Contract tests verify that recorded
provider examples match the DTOs and error mappings expected by Order.

Until provider services are stable, tests use a deterministic HTTP stub. Before
integration, the same examples must run against the real provider endpoints.

### Container smoke tests

Build the final image and verify:

- process starts with environment configuration;
- `/health/live` becomes healthy;
- `/health/ready` becomes healthy when SQL Server is available;
- the API reaches SQL Server by Docker service name;
- termination is graceful;
- no development-only dependency is required at runtime.

## 4. Test naming convention

Use behavior-oriented names:

```text
MethodOrUseCase_WhenCondition_ShouldExpectedOutcome
```

Examples:

```text
CreateOrder_WhenCartIsEmpty_ShouldReturnConflict
CreateOrder_WhenSecondStockDecrementFails_ShouldRestoreFirstItem
GetOrder_WhenCustomerDoesNotOwnOrder_ShouldReturnForbidden
UpdateTracking_WhenStepIsFive_ShouldSetCompletedStatus
```

Tests should follow Arrange-Act-Assert and contain one behavioral reason to fail.

## 5. Planned Red-Green-Refactor cycles

### Cycle 0 - Test infrastructure only

Red:

- empty test project is not yet connected to CI;
- API factory cannot start;
- SQL Server test container/migrations cannot be verified.

Green:

- create solution and test projects;
- wire test execution and deterministic configuration;
- add one health/smoke test.

Exit criteria:

- all suites can run locally and in CI;
- no business behavior implemented.

### Cycle 1 - Money and order-item invariants

Tests:

- quantity zero or negative is rejected;
- list price and discount cannot be negative;
- discount cannot exceed list price;
- final unit price is list price minus discount;
- line total is final unit price times quantity;
- decimal precision is preserved.

Production outcome:

- Money/value-object behavior and OrderItem construction.

### Cycle 2 - Order aggregate

Tests:

- empty item collection is rejected;
- total is the sum of line totals;
- customer ID is required and any future quote ID is optional in the interim model;
- tracking starts at step 0/pending;
- steps 0-2 map to pending, 3-4 to shipping, 5 to completed;
- values outside 0-5 are rejected;
- product snapshots remain unchanged after creation.

Production outcome:

- Order aggregate and tracking rules.

### Cycle 3 - Create request validation and authentication

Tests:

- missing JWT returns 401;
- missing idempotency key returns 400;
- invalid delivery address returns validation problem;
- customer ID from JSON is ignored/rejected;
- price/item fields are not part of the accepted request contract;
- customer identity comes from token claims;
- a valid RS256 access token with issuer `ganjj-authorization` authenticates;
- `sub` supplies a non-empty UUID account identity;
- `role == ADMIN` satisfies the Admin policy and `CLIENTE` does not;
- expired, tampered, wrong-issuer, and non-RS256 tokens return 401;
- a correctly signed refresh token with `typ == refresh` still returns 401;
- absent or malformed `sub` and absent `typ` return 401;
- a missing or invalid public-key file prevents unsafe startup.

Production outcome:

- API request model, controller boundary, and authentication setup.

Migration test sequence for the approved Authorization-contract change:

1. **Red:** replace legacy fixtures with an RSA key pair and provider-shaped
   tokens; observe failures caused by shared-key validation, `id`, and `isAdmin`.
2. **Green:** introduce only the RS256/public-key validation and new claim mapping
   required to pass the authentication tests.
3. **Red:** introduce UUID ownership, idempotency, response, and SQL mapping tests;
   observe failures while `CustomerId` is still numeric.
4. **Green:** change `CustomerId` to `Guid` through every layer and update the SQL
   schema to `uniqueidentifier`.
5. **Refactor:** centralize claim constants/token builders, remove legacy paths,
   and keep all existing checkout tests green.

### Cycle 4 - Successful checkout orchestration

Tests:

- Cart is called for the authenticated customer;
- a valid Cart snapshot creates one stock operation per item;
- order/items use exactly the Cart snapshots;
- Cart `price` maps to list price, `discounted_price` maps to final price, and
  the calculated total is `sum(discounted_price * quantity)`;
- SQL persistence occurs only after all stock operations succeed;
- completed idempotency result is stored;
- API returns 201 and Location header.

Production outcome:

- CreateOrder use case with ports and first happy-path persistence.

### Cycle 5 - Current Cart snapshot failures

Tests:

- empty cart maps to 409;
- missing/malformed items map to a controlled dependency-contract error;
- zero/negative quantity or price is rejected;
- arithmetic mismatch is rejected before stock changes;
- Cart timeout/unavailability maps to 503;
- no stock or database side effects occur in these cases.

Production outcome:

- Cart client mappings and snapshot validation.

### Cycle 6 - Stock Saga and compensation

Tests:

- first stock failure creates no order;
- second stock failure restores the first item;
- third stock failure restores prior items in reverse order;
- Product not-found maps to a controlled domain error;
- insufficient stock exposes safe availability context;
- restore failure is surfaced/logged as critical and is not swallowed;
- stable operation IDs are reused for idempotent dependency calls.

Production outcome:

- compensating Saga behavior and Product client mappings.

### Cycle 7 - SQL transaction failure

Tests:

- item insert failure rolls back order and all items;
- persistence failure after stock decrement restores every item;
- completed idempotency result does not survive rollback;
- successful transaction commits order, items, and idempotency together.

Production outcome:

- EF Core transaction boundary and failure handling.

### Cycle 8 - Idempotency

Tests:

- first key acquires processing ownership;
- completed duplicate returns the original response;
- duplicate in-progress request returns conflict without calling dependencies;
- same key with different request hash is rejected;
- confirmed compensated failure allows a safe retry;
- unresolved compensation retains a failed record and blocks blind replay.

Production outcome:

- SQL-backed idempotency behavior.

### Cycle 9 - Order queries and authorization

Tests:

- customer list contains only the authenticated customer's orders;
- results are newest first;
- customer can read their own order;
- customer cannot read another customer's order;
- comparison uses the exact UUID from JWT `sub`;
- `role == ADMIN` can read any order;
- missing order returns 404;
- snapshots are returned without live Product/Customer calls.

Production outcome:

- query use cases and owner/admin policy.

### Cycle 10 - Admin tracking update

Tests:

- non-admin receives 403;
- steps below 0 and above 5 return validation error;
- each valid step maps to the expected status;
- missing order returns 404;
- concurrent update with stale version is handled consistently.

Production outcome:

- update use case and optimistic concurrency behavior.

### Cycle 11 - Post-commit Support notification and transactional outbox

The direct HTTP `OrderCreated` notification remains active for Support. A durable
SQL outbox publishes `OrderConfirmed` to RabbitMQ for Promotion.

Tests:

- committed order sends the persisted `OrderCreated` event UUID to Support;
- Support failure or timeout leaves the order completed and does not compensate stock;
- the JWT e-mail and correlation ID reach the Support contract;
- Support deduplicates repeated delivery of the same event UUID.

Outbox tests:

- completed checkout persists Order and event atomically;
- successful dispatch marks the message processed;
- transient failure increments attempts and remains pending;
- repeated dispatch uses the same event identifier;
- the real broker receives persistent JSON with the agreed AMQP properties.

Production outcome:

- current post-commit HTTP notifier plus background RabbitMQ outbox dispatcher.

### Cycle 12 - Health, configuration, and containers

Tests:

- liveness does not depend on SQL Server;
- readiness is unhealthy when SQL Server is unavailable;
- readiness becomes healthy after database recovery;
- missing required production configuration prevents unsafe startup;
- API and SQL Server communicate through the Compose network;
- built image answers the smoke request.

Production outcome:

- operational endpoints, Dockerfile, and Compose setup.

### Cycle 13 - Provider contract verification

Tests:

- current `GET /api/cart` success example deserializes list price, discounted
  price, color, quantity, and opaque Product ID;
- Cart error examples map to expected Order errors;
- future quote/consume examples are tested only after group approval;
- Product decrement sends the English route/body and stable `Idempotency-Key`;
- Product restore sends `compensates_operation_id` for the original decrement;
- Authorization-shaped RS256 access-token fixtures authenticate with `sub`,
  `role`, `typ`, `iss`, and `exp`;
- the public key exposed by Authorization verifies the provider smoke token.

Production outcome:

- finalized provider DTOs and integration readiness.

## 6. Minimum critical test catalogue

| Test ID | Requirement | Expected level |
| --- | --- | --- |
| T-001 | FR-003/FR-005 | API + application |
| T-002 | FR-004 | Application + contract |
| T-003 | FR-006 | Application + contract |
| T-004 | FR-007 | Application |
| T-005 | FR-008 | SQL integration |
| T-006 | FR-009/FR-010 | SQL integration + application |
| T-007 | FR-011 | API integration |
| T-008 | FR-012 | API integration |
| T-009 | FR-013/FR-014 | API integration |
| T-010 | FR-015 | Domain unit |
| T-011 | FR-016 | API/OpenAPI validation |
| T-012 | FR-017 | Container smoke/integration |
| T-013 | NFR-001 | Architecture test/review |
| T-014 | NFR-002 | Configuration/container test |
| T-015 | NFR-004 | Application + HTTP stub |

## 7. Test data strategy

- Generate a unique database/schema context per test run.
- Use deterministic clocks and IDs in unit tests.
- Use builders/factories for valid Cart snapshots and orders.
- Override one field per invalid test to keep the failure reason clear.
- Do not depend on execution order.
- Do not share mutable Redis, Product, or Cart state across tests.
- Store no real credentials or personal data in fixtures.

## 8. External-service stubs

HTTP stubs must record:

- route and method;
- required authentication/internal headers;
- correlation and operation IDs;
- request payload;
- response status/body;
- number and order of calls.

Compensation tests must assert both that restore was called and that it received
the exact product, quantity, and stable operation identity.

## 9. Coverage and quality gates

Coverage is supporting evidence, not the goal. Proposed gates:

- 100% of identified critical failure branches have named tests;
- Domain and Application line coverage target of at least 80%;
- no merge when tests fail;
- no skipped critical test without a documented reason;
- integration tests use SQL Server semantics;
- OpenAPI is parsed/validated in CI;
- final Docker image passes its smoke test.

Mutation testing is optional and may be applied to money/status/compensation rules
if schedule permits.

## 10. Definition of done for each behavior

- A test was observed failing for the expected reason.
- Minimal code made it pass.
- Refactoring preserved green tests.
- Public behavior is represented in OpenAPI when applicable.
- Logs/errors contain no secrets or unsafe personal data.
- Relevant unit/integration/contract suites pass.
- Documentation is updated when a contract or decision changes.

## 11. Whole-service exit criteria

- All Must requirements in the PRD have automated evidence.
- Critical Saga and idempotency failure paths pass.
- SQL Server migration works from an empty database.
- API authorization tests pass for anonymous, customer, other customer, and admin.
- Provider contracts pass against agreed examples/real services.
- Docker Compose starts a healthy API and database.
- A versioned image passes on another machine/runtime.
- No implementation work remains hidden behind a disabled or fake test.
