# Proposta de adequação do Order ao Authorization

Status: Aprovada, implementada e validada por smoke cross-service
Data: 2026-08-29
Repositório provedor analisado: `Back-End-Cloud-Computing/authorization`

## 1. Objetivo

Adequar o Order ao contrato JWT já implementado pelo serviço Authorization, sem
alterar o Authorization e sem introduzir chamadas síncronas de autenticação em
cada requisição.

## 2. Contrato confirmado no Authorization

O access token:

- é assinado exclusivamente com `RS256`;
- possui emissor `ganjj-authorization`;
- armazena o identificador da conta em `sub`, como UUID;
- armazena o papel em `role`, com valor `CLIENTE` ou `ADMIN`;
- contém `typ: access` para diferenciá-lo do refresh token;
- contém `email`, `iat` e `exp`;
- é enviado em `Authorization: Bearer <token>`;
- pode ser validado localmente com a chave pública obtida em
  `GET /auth/public-key`.

O Authorization não emite as claims antigas `id` e `isAdmin`, não usa segredo
simétrico compartilhado e não define `aud` no contrato atual.

## 3. Diferenças encontradas no Order

| Aspecto | Order atual | Authorization | Alteração proposta |
| --- | --- | --- | --- |
| Assinatura | segredo simétrico | chave RSA privada/pública | validar com chave pública e fixar RS256 |
| Identidade | claim `id`, número positivo | claim `sub`, UUID | usar `sub` e `Guid` de ponta a ponta |
| Administrador | `isAdmin == true` | `role == ADMIN` | substituir a política administrativa |
| Tipo do token | não validado | `typ` distingue access/refresh | exigir `typ == access` |
| Emissor | opcional | `ganjj-authorization` | validar obrigatoriamente |
| Audience | opcional | não emitida | não validar até o provedor publicar `aud` |
| Transporte | Bearer e cookie legado | Bearer | manter somente Bearer no novo contrato |

## 4. Escopo de implementação após aprovação

1. Substituir `SigningKey` por `PublicKeyPath` na configuração do Order.
2. Carregar uma chave pública PEM no startup e falhar de forma segura quando ela
   estiver ausente ou inválida.
3. Configurar JWT Bearer para validar assinatura RS256, emissor, expiração e
   `typ == access`.
4. Extrair a identidade de `sub`, rejeitando UUID ausente, inválido ou vazio.
5. Autorizar rotas administrativas somente com `role == ADMIN`.
6. Trocar `CustomerId` de `long` para `Guid` no controller, aplicação, domínio,
   repositório, idempotência, respostas e testes.
7. Persistir `CustomerId` como `uniqueidentifier` em `Orders` e
   `IdempotencyRequests`.
8. Continuar encaminhando o access token Bearer original ao Cart, sem registrá-lo
   em logs.
9. Atualizar OpenAPI, exemplos de configuração, Docker/Compose e documentação.

## 5. Migração de dados

SQL Server não consegue deduzir qual UUID do Authorization corresponde a um
`CustomerId` numérico legado. Portanto:

- para bancos apenas de desenvolvimento, a recomendação é recriar o banco a
  partir da migration corrigida;
- para um banco que contenha pedidos que precisam ser preservados, será
  obrigatório fornecer uma tabela explícita `id numérico -> UUID da conta` e
  executar uma migração controlada;
- a implementação não inventará UUIDs, pois isso quebraria propriedade de pedido
  e idempotência.

## 6. Segurança

- O Order receberá somente a chave pública; a chave privada permanece no
  Authorization.
- O algoritmo aceito será fixado em RS256 para evitar downgrade/confusão de
  algoritmo.
- Refresh tokens serão recusados mesmo que possuam assinatura válida.
- A identidade e o papel nunca serão aceitos no corpo da requisição.
- Tokens e conteúdo de chaves não serão registrados em logs.
- Uma chave ausente ou inválida impedirá o startup em vez de desabilitar
  silenciosamente a validação.

## 7. Fora de escopo

- alterar emissão, login, refresh ou logout do Authorization;
- buscar a chave pública pela rede a cada requisição;
- implementar rotação/JWKS nesta etapa;
- alterar os contratos ainda pendentes de Cart, Promotion ou Product;
- converter `ProductId`, que pertence a outro contrato.

## 8. Critérios de aceite

- access token RS256 válido de cliente autentica e acessa apenas rotas de cliente;
- access token RS256 válido de admin satisfaz a política administrativa;
- token expirado, adulterado, de emissor diferente ou com algoritmo diferente
  retorna 401;
- refresh token assinado corretamente ainda retorna 401;
- `sub` ausente, inválido ou vazio retorna 401;
- cliente não acessa pedido de outro UUID;
- UUID é preservado no pedido e no registro de idempotência;
- o mesmo Bearer token chega ao Cart;
- testes unitários, de API, persistência e contrato ficam verdes;
- nenhum segredo privado é incluído no repositório ou na imagem.

## 9. Registro de aprovação

A proposta e o ADR 0006 foram aprovados em 2026-08-29. A implementação utiliza a
estratégia documentada de recriar o banco de desenvolvimento; qualquer ambiente
com dados que precisem ser preservados continua exigindo um mapa explícito de IDs.

O smoke cross-service foi concluído em 2026-08-29 com a chave pública e tokens
reais do Authorization: access token de administrador recebeu 200 nas rotas de
cliente e administrador, enquanto o refresh token recebeu 401.
