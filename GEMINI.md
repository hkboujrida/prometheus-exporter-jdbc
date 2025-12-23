# prometheus-exporter-jdbc Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-21

## Active Technologies
- Helm (v3+), YAML (002-helm-chart-github)
- GitHub Container Registry (OCI) (002-helm-chart-github)
- Helm v3+, YAML + Kubernetes 1.19+ (003-helm-auth-config)
- Kubernetes Secrets (for passwords) (003-helm-auth-config)
- Helm v3+, YAML, Java (Application) + Kubernetes 1.19+ (Pod Security Context) (004-security-fixes)
- N/A (Read-only filesystem enforced) (004-security-fixes)
- Java 8 + Jetty, Prometheus Client (005-auto-restart)
- YAML (Azure Pipelines), Helm v3+ + Azure DevOps, Harbor (006-azure-helm-push)
- Harbor OCI Registry (006-azure-helm-push)

- YAML (GitHub Actions), Docker (001-github-actions-docker-push)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for YAML (GitHub Actions), Docker

## Code Style

YAML (GitHub Actions), Docker: Follow standard conventions

## Recent Changes
- 006-azure-helm-push: Added YAML (Azure Pipelines), Helm v3+ + Azure DevOps, Harbor
- 005-auto-restart: Added Java 8 + Jetty, Prometheus Client
- 004-security-fixes: Added Helm v3+, YAML, Java (Application) + Kubernetes 1.19+ (Pod Security Context)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
