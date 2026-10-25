# Roteiro de apresentação — Order

## 1. Kubernetes e Helm

1. Coloque a chave pública do Authorization em `public.pem` na raiz do projeto.
2. Execute `./scripts/Start-Kubernetes.ps1`.
3. Mostre `helm list` e `kubectl get pods,deployments,statefulsets,services`.
4. Destaque as três réplicas do Order, o SQL Server em StatefulSet e os
   Services internos `ClusterIP`.
5. Execute o teste DNS e a exclusão controlada de um Pod descritos no README.
6. Explique que o Order não possui Ingress: o único host externo é
   `api.ganjj.local`, controlado pelo API Gateway.

## 2. Métricas e Grafana

Em um terminal, encaminhe temporariamente o Service interno:

```powershell
kubectl -n default port-forward service/order 3004:8080
```

Em outro terminal, inicie a stack de demonstração:

```powershell
docker compose -f observability/docker-compose.yml up -d
```

Gere algumas requisições e abra `http://localhost:3000/d/ganjj-order`. Mostre:

- requisições por segundo;
- taxa de respostas HTTP 5xx;
- respostas agrupadas por status;
- latências p50, p95 e p99.

O Prometheus coleta `/metrics` pelo acesso local temporário. Essa rota permanece
fora do Gateway e do Ingress, portanto não é uma API pública.

![Dashboard Grafana do Order](images/grafana-order-dashboard.png)

Ao terminar:

```powershell
docker compose -f observability/docker-compose.yml down
```
