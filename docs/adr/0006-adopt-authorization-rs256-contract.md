# ADR 0006 - Adotar o contrato JWT RS256 do Authorization

Status: Aceito
Data: 2026-08-29

## Contexto

O Order foi implementado provisoriamente contra o contrato legado do GANJJ:
assinatura simétrica, identidade numérica na claim `id` e autorização
administrativa por `isAdmin`. O serviço Authorization do grupo já substituiu
esse contrato por tokens RS256 com `sub` UUID, `role`, `typ`, emissor fixo e
validação local por chave pública.

Manter dois formatos criaria comportamento divergente entre os serviços e
permitiria que um token aceito pelo Order fosse recusado pelo restante do
sistema. Consultar o Authorization em toda requisição também adicionaria
latência e um ponto único de falha que o contrato novo evita deliberadamente.

## Decisão

- o Authorization será a fonte de verdade do contrato JWT;
- o Order aceitará somente access tokens RS256 emitidos por
  `ganjj-authorization`;
- a assinatura será validada localmente com uma chave pública PEM fornecida por
  configuração;
- `sub` será o identificador da conta e será representado como `Guid` em todas as
  camadas do Order;
- `role == ADMIN` satisfará a política administrativa;
- `typ == access` será obrigatório;
- `aud` não será exigida enquanto o Authorization não a emitir;
- o Order aceitará o token no header Bearer e o encaminhará ao Cart quando
  consultar a cesta.

## Consequências

- A integração passa a seguir o serviço realmente implementado pelo grupo.
- O Order deixa de compartilhar um segredo capaz de assinar tokens.
- Refresh tokens não podem ser usados como access tokens.
- `CustomerId` muda de `bigint` para `uniqueidentifier`, exigindo recriação do
  banco de desenvolvimento ou uma migração com mapeamento explícito para dados
  legados.
- A chave pública precisa ser distribuída ao container e à infraestrutura.
- Uma futura rotação transparente de chaves poderá exigir JWKS ou suporte a
  múltiplas chaves; isso fica fora desta decisão.

## Alternativas rejeitadas

### Manter compatibilidade simultânea com tokens antigos e novos

Rejeitada porque aumenta a superfície de segurança, perpetua um contrato sem
provedor e dificulta demonstrar qual identidade é autoritativa.

### Continuar usando `long` e criar um identificador local

Rejeitada porque exigiria uma tabela de tradução não fornecida pelo Auth e faria
o Order deixar de armazenar o identificador externo real da conta.

### Consultar o Authorization a cada requisição

Rejeitada porque contradiz o desenho de validação local, adiciona acoplamento
síncrono e impede autenticação quando o Authorization estiver temporariamente
indisponível.
