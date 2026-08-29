# GANJJ Order Service

Microsserviço de pedidos do projeto acadêmico GANJJ, implementado em ASP.NET
Core 10 e SQL Server. Ele cria um pedido a partir do carrinho autenticado,
reserva estoque no serviço de produtos e mantém um snapshot imutável dos itens.

## Documentação

- [PRD](docs/PRD.md) - requisitos, escopo e critérios de aceite.
- [SDD](docs/SDD.md) - arquitetura, dados, integrações, Docker e implantação.
- [Plano de TDD](docs/TDD-PLAN.md) - ciclos Red-Green-Refactor e estratégia de testes.
- [Contratos de integração](docs/INTEGRATION-CONTRACTS.md) - dependências entre serviços.
- [Pendências de integração do grupo](docs/INTEGRATION-ISSUES.md) - decisões em aberto, em português (PT-BR).
- [Contrato OpenAPI](docs/openapi/order-service.v1.yaml) - API pública proposta.
- [Plano de entrega](docs/DELIVERY-PLAN.md) - backlog e ordem de implementação.
- [ADRs](docs/adr/) - decisões arquiteturais registradas.

## Executar com Docker

Pré-requisito: Docker Desktop em execução.

Exporte a chave pública do Authorization para `public.pem` na raiz do projeto
antes de iniciar o Order. A chave privada nunca deve ser copiada.

```powershell
Copy-Item .env.example .env
docker compose up --build
```

A API estará em `http://localhost:3004`, a documentação Scalar em
`http://localhost:3004/scalar/v1` e os health checks em `/health/live` e
`/health/ready`. Ajuste os segredos do `.env` antes de compartilhar ou implantar.

## Executar e testar localmente

```powershell
dotnet tool restore
dotnet restore Ganjj.Order.sln
dotnet test Ganjj.Order.sln
dotnet run --project src/Ganjj.Order.Api
```

O teste de persistência usa Testcontainers e, portanto, também requer Docker.
Para execução local, configure `Authentication__Jwt__PublicKeyPath` com o caminho
absoluto do arquivo PEM. Para criar pedidos, envie um JWT contendo as
claims `sub`, `email`, `role` e `typ` do Authorization, além do header
`Idempotency-Key`. A assinatura RS256 é validada localmente com a chave pública.

## Estado

`Em implementação` - fundação, domínio, persistência, API, autenticação,
integrações HTTP, migrations, Docker e testes automatizados disponíveis.
