# ADR 0007 - Run the Order service on local Kubernetes

Status: Accepted
Date: 2026-09-16

## Context

The course now requires a Kubernetes demonstration. AWS publication is out of
scope because the course environment cannot provide the necessary permissions.
The lecture lab requires declarative Deployment and Service manifests, internal
DNS, three replicas, and recovery after a Pod is removed. ADR 0004 deferred
deployment manifests until the runtime was chosen; that deferral is now closed.

## Decision

- Use Minikube on Docker Desktop for the repeatable local demonstration.
- Run the stateless Order API as a three-replica Deployment behind a ClusterIP
  Service named `order`.
- Run SQL Server as a single-replica StatefulSet with an 8 GiB persistent claim
  and a Service named `order-sqlserver`; database replicas are not implied.
- Pull the published image described by ADR 0008 by default, with an explicit
  local-build fallback for development. Configure peer URLs using Service DNS
  names in the shared `default` namespace.
- Supply SQL credentials and the internal-service secret through a Kubernetes
  Secret. Mount only Authorization's public PEM through a ConfigMap.
- Permit runtime EF Core 10 migrations for this local course demo. EF Core's
  migration lock protects concurrent replicas; a one-shot migration Job and
  reduced schema permissions would be preferable for production.
- Expose the API to the Windows host via temporary `kubectl port-forward`.
  Ingress and AWS infrastructure are not required for this local milestone.

## Consequences

- The repository contains versioned manifests and a PowerShell bootstrap script.
- The presenter can show Service Discovery, three replicas, and Pod recovery
  without deploying the other microservices; full checkout still depends on
  Cart/Product integration contracts and running peers.
- The local SQL Server PVC retains data across Pod replacement. Deleting the
  cluster or PVC is a separate, explicit data-loss operation.
- Image publication is independent from AWS and exists only to make the local
  Kubernetes demonstration reproducible across the group's machines.
