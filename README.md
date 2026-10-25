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

## Executar com Kubernetes local

Pré-requisitos: Docker Desktop com containers Linux, Minikube, `kubectl` e a
chave pública RS256 do Authorization em `public.pem` (raiz do projeto). A chave
privada não deve ser copiada. Reserve pelo menos 4 CPUs, 6 GiB para o Minikube
e espaço para o volume de 8 GiB do SQL Server.

```powershell
# Opcional: defina INTERNAL_SERVICE_SECRET com o valor combinado pelo grupo.
.\scripts\Start-Kubernetes.ps1
```

O script inicia o cluster local se necessário, cria um Secret com senha forte
gerada localmente (ou usa `SQL_SERVER_PASSWORD`), monta a chave pública como
ConfigMap, baixa a imagem pública multi-arquitetura do GHCR e espera o SQL Server
e as três réplicas do Order ficarem prontos. Ao rodar novamente, ele preserva a
senha do banco persistente; `INTERNAL_SERVICE_SECRET` pode ser alterado para
combinar com o grupo. Nenhum valor secreto é versionado no Git.

Para testar alterações ainda não publicadas, use o fallback local:

```powershell
.\scripts\Start-Kubernetes.ps1 -UseLocalImage
```

```powershell
kubectl -n default get pods,deployments,statefulsets,services -l app.kubernetes.io/part-of=ganjj
kubectl -n default port-forward service/order 3004:8080
```

Em outro terminal, acesse `http://localhost:3004/scalar/v1`,
`http://localhost:3004/health`, `http://localhost:3004/health/ready` e
`http://localhost:3004/instance`.
O endpoint `/instance` identifica o Pod que atendeu a requisição.
`GET /metrics` expõe contadores e latência no formato Prometheus apenas pelo
Service interno ou pelo `port-forward`; o Gateway não encaminha essa rota.

Para demonstrar o DNS interno e a autorrecuperação:

```powershell
kubectl -n default run order-dns-check --image=curlimages/curl:8.11.1 --restart=Never --rm -i -- curl -fsS http://order:8080/health/ready
kubectl -n default run order-replica-check --image=curlimages/curl:8.11.1 --restart=Never --rm -i -- sh -c 'for i in 1 2 3 4 5 6 7 8 9 10; do curl -fsS http://order:8080/instance; echo; done'
$pod = kubectl -n default get pods -l app.kubernetes.io/name=order -o jsonpath='{.items[0].metadata.name}'
kubectl -n default delete pod $pod
kubectl -n default rollout status deployment/order
kubectl -n default get pods -l app.kubernetes.io/name=order
```

As chamadas a `/instance` criam conexões independentes e podem mostrar Pods
diferentes; o Service não garante um round-robin rígido. O `port-forward` é
apenas uma entrada temporária e não serve para demonstrar distribuição entre
réplicas.

`order` e `order-sqlserver` são Services internos (`ClusterIP`) no namespace
`default`. As URLs de Cart, Product e Support no manifesto pressupõem Services
`shopping-cart:3003`, `product-api:8000` e `support:8090` nesse mesmo namespace; sem Cart e Product,
health/Scalar funcionam, mas o checkout integrado não. Esta implantação é
exclusivamente local, sem AWS. O volume do SQL Server é persistente; não apague
o PVC para reiniciar a apresentação.

Após confirmar o pedido, o Order tenta enviar `OrderCreated` ao Support. A falha
ou demora do Support é apenas registrada em log e não desfaz o pedido. Para a
demonstração de e-mail no Mailpit, use o mesmo `INTERNAL_SERVICE_SECRET` nos dois
serviços.

## Executar com Docker

Pré-requisito: Docker Desktop em execução.

Exporte a chave pública do Authorization para `public.pem` na raiz do projeto
antes de iniciar o Order. A chave privada nunca deve ser copiada.

```powershell
Copy-Item .env.example .env
docker compose up --build
```

A API estará em `http://localhost:3004`, a documentação Scalar em
`http://localhost:3004/scalar/v1` e os health checks em `/health`,
`/health/live` e `/health/ready`. As métricas ficam em `/metrics`. Ajuste os segredos do `.env` antes de
compartilhar ou implantar.

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
integrações HTTP, migrations, Docker, Kubernetes local e testes automatizados
disponíveis; integração de checkout depende dos contratos dos demais serviços.
