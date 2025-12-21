# Research: Helm Chart & OCI Publishing

**Feature**: Helm Chart Creation & Publishing
**Date**: 2025-12-21

## Key Decisions

### 1. Helm Chart Structure
**Decision**: Follow the standard Helm v3 starter template structure.
**Rationale**: Ensures familiarity for Kubernetes administrators and compatibility with standard tooling.
**Components**:
- `Chart.yaml`: Metadata
- `values.yaml`: Default configuration (image, port, config.json)
- `templates/`: Manifests (Deployment, Service, ConfigMap)

### 2. Configuration Injection
**Decision**: Use a ConfigMap to inject `config.json` content.
**Rationale**: `config.json` is the application's primary configuration source. Helm's `values.yaml` will expose a `config` key that is serialized into the ConfigMap, satisfying Principle I (Configuration-First).

### 3. Hosting Mechanism
**Decision**: GitHub Container Registry (OCI).
**Rationale**: 
- **Modern Standard**: Helm v3.8+ supports OCI registries natively.
- **Simplicity**: Reuses existing GHCR infrastructure and authentication (`GITHUB_TOKEN`).
- **Unified Artifacts**: Docker images and Helm charts coexist in the same registry namespace.

### 4. Automation Workflow
**Decision**: Use `helm/chart-releaser-action` or direct OCI push commands in GitHub Actions.
**Refinement**: Since OCI is chosen, `helm push` is the native command.
**Workflow**:
1. Checkout
2. Login to GHCR (`helm registry login`)
3. Lint (`helm lint`)
4. Package (`helm package`)
5. Push (`helm push`)

**Alternatives Considered**:
- *GitHub Pages (traditional repo)*: Rejected in favor of OCI for modernization and simpler maintenance (no `index.yaml` management).
