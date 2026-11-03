# Evidência E2E — checkout com cupom pelo Gateway

- Data da execução: 03/11/2026
- Ambiente: Minikube local, com Ingress NGINX
- Entrada pública utilizada: `http://api.ganjj.local`
- Issue relacionada: [#12](https://github.com/Back-End-Cloud-Computing/order/issues/12)

## Objetivo

Comprovar, com os serviços reais integrados, que:

1. o Authorization fornece um token aceito pelo Gateway e pelo Order;
2. o Cart entrega ao Order os preços finais e o cupom aplicado;
3. o total persistido no Order coincide com o total do Cart;
4. repetir a mesma `Idempotency-Key` não cria outro pedido nem reduz o estoque novamente;
5. o evento `OrderConfirmed` consome o cupom uma única vez no Promotion;
6. o evento `OrderCreated` produz uma única notificação no Mailpit;
7. as filas e DLQs terminam sem mensagens pendentes.

O fluxo de negócio entrou exclusivamente pelo Ingress do API Gateway. Os acessos
diretos com `kubectl` foram usados somente depois da compra para inspecionar
filas, registros persistidos e logs.

## Componentes reais

| Componente | Imagem executada |
| --- | --- |
| API Gateway | `bascratki/api-gateway:1.1.1` |
| Authorization | `eduardofabri/authorization:1.2.1` |
| Product | `joao2006/product:1.1.0` |
| Shopping Cart | `jcliz/shopping-cart:1.1.1` |
| Order | `ghcr.io/back-end-cloud-computing/order:latest` (merge `eee07a6`) |
| Promotion e consumer | `bascratki/promotion:1.2.0` |
| Support | `eduardofabri/support:1.0.3` |

Também foram usados SQL Server, Redis, MySQL, RabbitMQ e Mailpit reais dentro do
cluster. Não foram usados fixtures, stubs ou chamadas diretas aos serviços para
executar a compra.

## Cenário executado

1. Login real como administrador pelo Authorization.
2. Limpeza do carrinho de teste.
3. Criação de um produto com preço unitário de `100.00` e estoque inicial `10`.
4. Inclusão de duas unidades no Cart.
5. Aplicação do cupom `BEMVINDO10`.
6. Consulta do snapshot persistido do Cart.
7. Criação do pedido com a chave `order-coupon-1793736140`.
8. Repetição do mesmo `POST /api/pedidos`, com a mesma chave e o mesmo corpo.
9. Inspeção do Product, Promotion, RabbitMQ, Support e Mailpit.

## Resultado síncrono

| Verificação | Resultado |
| --- | --- |
| Login | HTTP 200 |
| Produto | HTTP 201; ID `6aea3dccecc56e4431dde089` |
| Item no carrinho | HTTP 201 |
| Cupom | HTTP 200; `code=BEMVINDO10`; `applied=true` |
| Subtotal do Cart | `200.00` |
| Desconto do Cart | `20.00` |
| Total do Cart | `180.00` |
| Primeiro checkout | HTTP 201; pedido `0a45f5aa-78a7-415e-806b-c67e3ee041b6`; total `180.00` |
| Checkout repetido | HTTP 201; mesmo pedido `0a45f5aa-78a7-415e-806b-c67e3ee041b6`; total `180.00` |
| Estoque após as duas chamadas | `8` |
| Pedido em `/api/pedidos/meus` | encontrado |

O Order preservou o preço de lista e somou os `discounted_price` fornecidos pelo
Cart. Portanto, o desconto não foi recalculado pelo Order e nenhum preço veio do
corpo enviado pelo cliente.

## Resultado assíncrono

Antes desta execução, o contador do cupom era `1`, devido a uma execução de
controle anterior. Depois da criação e da repetição idempotente, o contador era
`2`, e não `3`. Para o novo pedido, existia exatamente um registro em
`processed_orders`:

```json
{
  "order_id": "0a45f5aa-78a7-415e-806b-c67e3ee041b6",
  "coupon_code": "BEMVINDO10",
  "outcome": "consumed"
}
```

O consumer do Promotion registrou o pedido uma vez com `outcome=consumed`. O
Support registrou um envio de e-mail para o evento correspondente. No Mailpit,
foi encontrada exatamente uma mensagem com o assunto:

```text
Pedido #GNJ-20261103-0A45F5AA confirmado
```

Estado final do RabbitMQ:

| Fila | Mensagens | Consumers |
| --- | ---: | ---: |
| `promotion.order-confirmed` | 0 | 1 |
| `promotion.order-confirmed.dlq` | 0 | 0 |
| `support.order-created` | 0 | 1 |
| `support.order-created.dlq` | 0 | 0 |

## Conclusão

O critério funcional da issue #12 foi atendido no Kubernetes local: pedido
criado por `api.ganjj.local`, total do Order igual ao total com desconto do Cart,
cupom consumido uma única vez, estoque decrementado uma única vez e exatamente
um e-mail visível no Mailpit. A execução também comprovou que o Gateway continua
sendo a única entrada pública do fluxo.

Esta evidência local não afirma execução no Amazon EKS. Uma eventual demonstração
em ambiente compartilhado continua sendo uma atividade operacional separada.
