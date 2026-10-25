# Grafana dashboard

`order-dashboard.json` is the versioned dashboard for the Order service. It
uses the metrics emitted by `prometheus-net.AspNetCore` at `/metrics` and shows:

- request throughput;
- HTTP 5xx error rate;
- response status distribution;
- p50, p95 and p99 request latency.

Import the JSON through **Dashboards > New > Import**. It references the stable
datasource UID `prometheus`, which the supplied provisioning file creates, so no
panel or query needs manual editing. Prometheus must scrape `/metrics`; the
endpoint is intentionally not exposed by the API Gateway.

The dashboard filters by the Prometheus `job` label and selects all discovered
Order scrape jobs by default.

For a local preview, first expose Order on port 3004 and then start the supplied
stack:

```powershell
kubectl -n default port-forward service/order 3004:8080
docker compose -f observability/docker-compose.yml up -d
```

Open `http://localhost:3000/d/ganjj-order`. Grafana uses anonymous Viewer access
only in this local demonstration stack.
