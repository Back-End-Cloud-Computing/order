# Delivery Plan - GANJJ Order Service

Status: Proposed  
Last updated: 2026-08-21

## 1. Delivery policy

Implementation starts only after PRD/SDD review and uses the ordered TDD cycles in
`TDD-PLAN.md`. Each phase must leave a demonstrable, green state.

No phase may introduce shared-database access or client-supplied prices as a
temporary shortcut.

## 2. Milestones

### M0 - Planning approval

Deliverables:

- PRD reviewed by Rodrigo/group;
- SDD reviewed;
- OpenAPI proposal reviewed;
- Cart and Product contract proposals sent to their owners;
- pending group decisions visibly recorded.

Exit criteria:

- no unresolved disagreement about Order's boundary or pricing ownership.

### M1 - Executable skeleton

TDD cycles: 0  
Scope:

- solution and four production projects;
- test projects and CI test command;
- API startup, Problem Details, OpenAPI, Scalar UI, correlation ID;
- liveness endpoint;
- no order business behavior yet.

Demo:

- application starts and the first test suite passes.

### M2 - Domain complete

TDD cycles: 1-2  
Scope:

- money, delivery address, order item, order aggregate;
- totals and tracking/status rules;
- no database or HTTP dependency.

Demo:

- domain tests describe all business invariants.

### M3 - SQL Server persistence and query API

TDD cycles: relevant portions of 7, 9, and 10  
Scope:

- EF Core model/migrations;
- SQL Server integration tests;
- order snapshot persistence and transaction;
- list/detail/admin tracking endpoints against seeded test data.

Demo:

- API reads/writes its own SQL Server with no external database joins.

### M4 - Checkout against deterministic stubs

TDD cycles: 3-8  
Scope:

- JWT boundary;
- current Cart snapshot client;
- Product stock client;
- Saga compensation;
- idempotency;
- full create-order endpoint against HTTP stubs.

Demo:

- happy checkout, insufficient stock, database failure, and duplicate request.

### M5 - Cross-service integration

TDD cycles: 11 and 13  
Dependencies:

- current Cart read contract available;
- Product decrement/restore idempotency implemented;
- JWT contract frozen;
- gateway headers/routing available.

Scope:

- provider examples replaced/verified against real services;
- end-to-end checkout on the Docker network;
- Cart remains unchanged after order creation until safe versioned consumption is
  approved;
- real compensation test in a controlled environment.

Demo:

- authenticated request passes through Gateway and creates a durable SQL Server
  order from Redis Cart data.

### M6 - Docker/registry milestone

TDD cycle: 12  
Scope:

- multi-stage Dockerfile;
- API and SQL Server Compose services;
- named network/volume and health checks;
- `.env.example` and documented commands;
- explicit image tags;
- push/pull from selected registry.

Demo aligned with professor's material:

1. build artifact;
2. build image;
3. run API and database containers;
4. show DNS by service/container name;
5. show environment configuration;
6. tag and push image;
7. pull/run on another environment.

### M7 - AWS/cloud integration

Blocked until the group selects ECS, EKS, or EC2.

Common scope regardless of runtime:

- registry mapping to Docker Hub/ECR;
- secret injection;
- central logs;
- health-based deployment;
- database connectivity and persistence;
- smoke test through public gateway/load balancer;
- teardown/cost-control instructions.

### M8 - Final hardening and presentation

Scope:

- all quality gates green;
- OpenAPI matches implementation;
- architecture diagrams match deployment;
- known limitations documented;
- support-microservice group decision incorporated;
- presentation evidence collected.

## 3. Implementation backlog

| Order | Backlog item | Depends on | Evidence |
| --- | --- | --- | --- |
| 1 | Scaffold solution/test projects | Planning approval | CI/test command green |
| 2 | Domain money/item/order rules | None | Unit tests |
| 3 | API error/auth skeleton | JWT example | API tests |
| 4 | EF Core SQL Server mapping/migration | Domain | Container-backed tests |
| 5 | Query endpoints/authorization | Persistence | API integration tests |
| 6 | Idempotency store | SQL Server | Concurrency/duplicate tests |
| 7 | Current Cart snapshot client | Cart examples | Consumer contract tests |
| 8 | Product stock client | Product examples | Consumer contract tests |
| 9 | Checkout Saga | Items 6-8 | Application failure tests |
| 10 | Atomic order persistence | Checkout Saga | Rollback tests |
| 11 | Future outbox/Cart quote consumption | Group approval | Deferred dispatcher tests |
| 12 | Dockerfile/Compose/health | API + migration | Container smoke tests |
| 13 | Real cross-service checkout | Provider implementations | End-to-end evidence |
| 14 | Registry publication | Docker image | Pull/run proof |
| 15 | AWS runtime deployment | Group runtime decision | Public smoke test/logs |

## 4. Cross-team action list

### João Liz - Shopping Cart

- review `INTEGRATION-CONTRACTS.md` Cart section;
- add cart versioning;
- calculate the Product/Promotion checkout quote;
- implement quote expiration and idempotency;
- implement consume/clear-if-version-matches;
- provide JSON success/error examples.

### João Correa - Product

- confirm product ID type;
- agree decrement/restore routes;
- make operations idempotent by stable operation ID;
- provide insufficient-stock/not-found examples.

### Eduardo Fabri - Customer/Auth

- freeze JWT algorithm, issuer, audience, and claims;
- provide customer and admin example tokens/claims;
- confirm whether name/email snapshots come from JWT.

### Lucas Stopinski - Promotion

- agree how Cart obtains active promotion/final price;
- provide promotion snapshot fields required for order history.

### Group

- select support microservice;
- select final AWS runtime and registry;
- define gateway ownership/routing;
- agree shared correlation/internal-auth conventions.

## 5. Git workflow proposal

- Keep `main` releasable.
- Create small branches prefixed with `codex/` only when Codex creates branches;
  otherwise follow the group's agreed naming convention.
- One TDD behavior or cohesive cycle per change set where practical.
- Pull requests include tests and contract/document changes.
- Never commit `.env`, passwords, JWT secrets, or AWS credentials.
- Version releases with semantic tags such as `v1.0.0` and matching image tags.

## 6. Definition of ready for implementation

- [ ] Rodrigo approves PRD scope.
- [ ] Rodrigo approves SDD architecture.
- [ ] Cart owner acknowledges required quote work.
- [ ] Product owner acknowledges stock contract direction.
- [ ] Initial JWT example/claim direction exists.
- [ ] OpenAPI create-order shape is accepted.
- [ ] No code implementation is expected to preserve client-supplied price.

## 7. Definition of done for the service

- [ ] All Must PRD requirements implemented.
- [ ] TDD critical catalogue green.
- [ ] Migrations create an empty SQL Server database successfully.
- [ ] No external table/database access.
- [ ] End-to-end checkout succeeds with real Cart and Product.
- [ ] Failure/compensation demonstration succeeds.
- [ ] API and SQL Server run in separate containers.
- [ ] Configuration comes from environment variables.
- [ ] Versioned image is published and pullable.
- [ ] Health/log evidence is available.
- [ ] AWS deployment matches final group decision.
- [ ] Support-microservice decision is reflected at group architecture level.

## 8. Professor-rubric traceability

| Rubric theme | Planned evidence |
| --- | --- |
| No frontend required | REST/OpenAPI demonstrations only |
| Distinct domain languages | Order shown as C# alongside team services |
| Distinct databases | Order owns SQL Server; no shared schema |
| Support microservice | Explicit group decision remains tracked |
| Cloud techniques | containers, network, env config, health, registry, AWS path |
| Docker artifact flow | source -> publish artifact -> image -> container -> registry |
| Microservice integration | Current Cart snapshot + Product stock Saga |
| Portability | same image/config model locally and in cloud |
| Reliability | idempotency, compensation, outbox, tests |

## 9. Known non-blocking decisions

- Support microservice type/owner.
- AWS compute/orchestrator.
- Docker Hub versus ECR for final image publication.
- Optional event broker/`OrderCreated` consumer.

These decisions must close before M7/M8, not before M1-M4.
