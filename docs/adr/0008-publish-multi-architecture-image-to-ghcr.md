# ADR 0008 - Publish the multi-architecture Order image to GHCR

Status: Accepted
Date: 2026-10-25

The machine-local image from ADR 0007 prevented teammates from deploying the
same manifest and did not cover both Intel/AMD and Apple Silicon machines. CI
publishes `ghcr.io/back-end-cloud-computing/order:latest` and an immutable commit
tag for `linux/amd64` and `linux/arm64`; Kubernetes pulls that public image by
default, while the bootstrap script retains an explicit local-build fallback.
This keeps execution local and reproducible without introducing AWS, at the cost
of depending on GHCR availability and public package visibility.
