# ADR 0004 - Build a portable, versioned container before choosing AWS runtime

Status: Superseded for deployment by ADR 0007
Date: 2026-08-21

The original AWS/runtime deferral below records the decision at that time. The
current course deployment target is local Kubernetes; see ADR 0007.

## Context

Course material progresses from executable artifact to Docker image, registry,
and Kubernetes. The group will use AWS, but has not fixed ECS, EKS, or EC2.

## Decision

Build one stateless, environment-configured .NET container and run SQL Server in
a separate local container. Use explicit immutable image tags. Docker Hub is the
initial registry-compatible path; ECR can replace it later. AWS infrastructure
and Kubernetes manifests are deferred until the group/course confirms runtime.

## Consequences

- Application code is not coupled to an AWS compute product.
- Compose supports local demonstration of network and environment concepts.
- Health endpoints and graceful termination are required now.
- Deployment-specific manifests remain a later deliverable.
