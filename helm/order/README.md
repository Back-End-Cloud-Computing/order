# Chart Helm — `order`

Empacota o mesmo desenho de `k8s/`: três réplicas do Order, Service interno,
SQL Server opcional em StatefulSet com PVC, probes, recursos e scrape do
Prometheus. O chart não cria Ingress; `api.ganjj.local` pertence ao API Gateway,
que é a única entrada externa aceita pelo grupo.

Segredos e a chave pública JWT continuam fora do chart. Antes da instalação,
crie:

- `Secret/order-secrets` com `connection-string`, `sqlserver-sa-password` e
  `internal-secret`;
- `ConfigMap/order-jwt-public-key` com a chave `public.pem` exportada pelo
  Authorization;
- `Secret/rabbitmq-credentials` com `username` e `password`, além do
  `Service/rabbitmq`, quando `rabbitMq.enabled=true`.

O script `scripts/Start-Kubernetes.ps1` já cria esses dois recursos com valores
locais seguros, instala o release e chama `scripts/Test-Kubernetes.ps1` para
validar o ambiente. Antes da instalação, ele também recusa recursos legados com
os mesmos nomes que ainda não pertencem ao release Helm, evitando adoção ou
remoção silenciosa. Para criar apenas a ConfigMap manualmente:

```powershell
kubectl create configmap order-jwt-public-key --from-file=public.pem=./public.pem
```

Validação e instalação:

```powershell
helm lint helm/order
helm template order helm/order
helm upgrade --install order helm/order
kubectl rollout status statefulset/order-sqlserver --timeout=600s
kubectl rollout status deployment/order --timeout=360s
.\scripts\Test-Kubernetes.ps1
```

Valores comuns:

| Necessidade | Opção |
| --- | --- |
| Imagem/tag diferente | `--set image.repository=... --set image.tag=...` |
| Número de réplicas | `--set replicaCount=3` |
| Host público do Gateway | `--set gateway.host=api.ganjj.local` |
| Recursos da API | arquivo de values com `resources` |
| Secret existente | `--set existingSecret=...` |
| ConfigMap JWT existente | `--set existingJwtConfigMap=...` |
| Banco externo | `--set sqlServer.enabled=false` e Secret com outra connection string |
| URLs dos pares | `--set services.cartBaseUrl=...` (idem Product e Support) |
| Order isolado, sem broker | `--set rabbitMq.enabled=false` (eventos ficam no outbox) |

`gateway.host` documenta o host público esperado e aparece nas instruções após
a instalação. Ele não cria um Ingress. O roteamento de `api.ganjj.local`
continua sendo responsabilidade do chart/repositório do Gateway; adicionar um
host direto ao Order contornaria autenticação de borda, rate limit e rastreio.
