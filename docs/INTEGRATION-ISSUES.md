# Pendências de integração entre os microsserviços GANJJ

Status: Em discussão pelo grupo  
Última atualização: 2026-09-16
Idioma: Português (PT-BR)

## 1. Objetivo

Este documento reúne problemas e decisões que afetam mais de um microsserviço.
Ele não substitui os contratos técnicos aprovados em
`INTEGRATION-CONTRACTS.md`. Depois que uma decisão for tomada pelo grupo, o
contrato correspondente deverá ser atualizado e esta pendência marcada como
resolvida.

## 2. Resumo

| ID | Tema | Serviços afetados | Situação | Bloqueia agora? |
| --- | --- | --- | --- | --- |
| INT-001 | Contrato JWT | Auth, Gateway e todos os serviços | Resolvido no Authorization e Order | Não |
| INT-002 | Cotação final do carrinho | Cart, Product, Promotion e Order | Pendente | Não para desenvolvimento; sim para integração final |
| INT-003 | Limpeza segura do carrinho | Cart e Order | Pendente | Não |
| INT-004 | Operações idempotentes de estoque | Product e Order | Pendente | Não para stubs; sim para retries seguros |
| INT-005 | Tipos dos identificadores | Todos os serviços | Pendente | Não |
| INT-006 | Autenticação interna | Gateway e serviços | Pendente | Não |
| INT-007 | Convenção de nomes e rotas | Todos os serviços | Parcialmente decidido | Não |
| INT-008 | Microsserviço de suporte | Eduardo / Order | Resolvido para N2 | Não |
| PROMO-001 | Evento de pedido confirmado | Order e Promotion | Contrato resolvido; código pendente | Sim para RabbitMQ |

## 3. Pendências detalhadas

### INT-001 — Contrato JWT

**Situação atual:** o serviço Authorization implementou RS256, emissor
`ganjj-authorization`, UUID em `sub`, `role` (`CLIENTE`/`ADMIN`), `typ` e Bearer
token. A chave pública é publicada por `GET /auth/public-key`. O Order ainda usa
o contrato legado `id`/`isAdmin` e segredo simétrico.

**Decisão do Order:** adotar o contrato implementado pelo Authorization conforme
ADR 0006, trocar `CustomerId` para UUID e remover a aceitação do contrato legado.
A implementação e os testes locais foram concluídos.

**Validado em smoke cross-service:**

- chave pública real do Authorization validada pelo Order;
- access token real aceito nas rotas de cliente e administrador;
- UUID de `sub` aceito na consulta ao SQL Server;
- refresh token real recusado com 401.

**Ainda precisa ser confirmado para o ambiente final:**

- distribuição da chave pública PEM entre os Pods no Kubernetes local (o Order
  já a monta em um ConfigMap próprio; os demais serviços precisam confirmar);
- encaminhamento do header `Authorization` pelo Gateway e aceitação pelo Cart.

**Responsável pela decisão:** Eduardo (Auth/Cliente), com validação do grupo.

### INT-002 — Cotação final do carrinho

**Situação atual:** o Cart está implementado em `GET /api/cart` e compõe os dados
atuais do Product com os descontos do Promotion. A resposta possui `product_id`,
`name`, `price`, `color`, `image_url`, `quantity` e `discounted_price`, além dos
totais do carrinho.

**Problema restante:** o Cart ainda não persiste versão nem cotação de checkout
com expiração. O cupom é calculado somente quando recebido por query string e não
fica associado a uma cotação que o Order possa consumir com segurança.

**Decisão provisória do Order:** chamar `GET /api/cart`, persistir `price` como
preço de lista e `discounted_price` como preço final e calcular
`discounted_price * quantity`. O Order não aplicará regras de promoção e não
consumirá cupom até existir uma cotação persistida/autoritativa.

**Decisão que o grupo precisa tomar:**

1. Cart passa a produzir e consumir uma cotação final versionada; ou
2. o grupo aceita formalmente o snapshot promocional sem cupom para a
   apresentação acadêmica.

**Recomendação:** Cart deve produzir a cotação final, consultando Product e
Promotion, porque Order não deve conhecer regras de preço ou promoção.

**Responsáveis:** João Liz (Cart), João Correa (Product), Lucas Stopinski
(Promotion) e Rodrigo Alves (Order).

### INT-003 — Limpeza segura do carrinho

**Situação atual:** o Cart não possui versão ou operação de consumo de checkout
que o Order possa usar após persistir o pedido.

**Risco:** se o cliente adicionar outro produto enquanto o pedido é criado, uma
limpeza posterior pode apagar também o item novo.

**Decisão provisória do Order:** não limpar automaticamente o Cart depois da
criação do pedido.

**Recomendação:** adicionar versão ao carrinho e limpar somente a versão usada
na compra.

**Responsáveis:** João Liz (Cart) e Rodrigo Alves (Order).

### INT-004 — Estoque idempotente e compensação

**Situação atual:** resolvida no contrato entre Order e Product. O Product expõe
`/internal/products/{id}/stock/decrement` e `/stock/restore`, recebe
`Idempotency-Key` e elimina repetição da mesma operação.

**Risco:** uma repetição causada por timeout pode diminuir ou restaurar o estoque
mais de uma vez.

**Implementação no Order:** cada decremento e restauração recebe uma chave estável;
a restauração também envia `compensates_operation_id`. O Order não faz repetição
cega de POST por timeout.

**Responsáveis:** João Correa (Product) e Rodrigo Alves (Order).

### INT-005 — Tipos dos identificadores

**Situação atual:** resolvida para Product/Order. `product_id` é um texto opaco de
até 64 caracteres; o Order não interpreta nem converte esse valor para número.

**Ainda pendente:** confirmar os tipos externos de `promotion_id` e de uma futura
`cart/quote_id`. `customer_id` e `order_id` são UUIDs; `product_id` é texto.

**Responsável:** todo o grupo.

### INT-006 — Autenticação das chamadas internas

**Situação proposta:** utilizar inicialmente o header `x-internal-secret` nas
rotas internas.

**Decisão necessária:** definir quem gera o segredo, como ele chega aos
Pods via Kubernetes Secrets e quais endpoints exigem esse header. Segredos não devem ser
gravados no GitHub ou em imagens Docker.

**Responsável:** todo o grupo.

### INT-007 — Convenção de nomes e rotas

**Decisão já indicada pelo grupo:** novo código, classes, propriedades e payloads
devem usar inglês.

**Decisão aplicada nas integrações atuais:** o Cart publica `/api/cart` e o
Product publica `/internal/products/...`; o Order mantém `/api/pedidos` como rota
pública por compatibilidade. O Gateway continua sendo o ponto público de entrada.

**Responsável:** todo o grupo.

### INT-008 — Microsserviço de suporte

**Situação atual:** o serviço Support do Eduardo foi escolhido. Para a N2, ele
recebe o payload `OrderCreated` por HTTP interno e elimina duplicidade pelo
`event_id`.

**Impacto no Order:** após confirmar a transação, o Order chama
`POST http://support:8090/internal/notifications` com o segredo compartilhado.
O UUID do evento fica salvo com o pedido; falha ou demora do Support gera log,
mas não desfaz o pedido. O telefone fica ausente até existir contrato estável do
Client. RabbitMQ poderá substituir o transporte mantendo o mesmo payload.

**Responsável:** Eduardo (Support) e Rodrigo (integração do Order).

### PROMO-001 — Evento `OrderConfirmed` para o Promotion

**Situação atual:** contrato fechado nos dois repositórios em 25/10/2026. A
exchange durável é `ganjj.orders` (direct), a routing key é `order.confirmed` e
a fila do Promotion é `promotion.order-confirmed`. O payload versionado usa
nomes em inglês e contém `event_id`, `order_id`, `coupon_code`, itens e
`correlation_id`. A idempotência do consumidor é por `order_id`.

**Ainda pendente:** o Order atual consome o contrato antigo do Cart e não recebe
nem salva o cupom. O publisher com outbox só pode ser habilitado depois que a
cotação autoritativa do Cart fornecer `coupon_code`. Do outro lado, o Promotion
ainda precisa implementar o consumidor e o registro idempotente. O Order não
deve aceitar cupom diretamente no JSON público para contornar esse bloqueio.

**Responsáveis:** Rodrigo Alves (publisher/outbox) e Lucas Stopinski
(consumer/idempotência).

## 4. Checklist para a reunião do grupo

- [x] Aprovar o ADR 0006 e o contrato JWT (`sub`, `email`, `role`, `typ`).
- [ ] Compartilhar um access token de teste sem expor chave privada.
- [ ] Decidir quem calcula o preço final e as promoções no checkout.
- [ ] Decidir se o Cart terá cotação e versionamento.
- [ ] Aprovar as operações de decremento/restauração de estoque.
- [ ] Padronizar os tipos de IDs externos.
- [ ] Padronizar autenticação e headers internos.
- [ ] Decidir a estratégia de nomes das rotas públicas e internas.
- [x] Escolher o microsserviço de suporte e congelar o contrato HTTP da N2.
- [x] Fechar o contrato RabbitMQ `OrderConfirmed` com o Promotion.
- [ ] Implementar e testar publisher/outbox e consumer idempotente.
