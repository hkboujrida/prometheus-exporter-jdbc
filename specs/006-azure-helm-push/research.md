# Research: Azure DevOps Helm Chart Push to Harbor

**Context**: Automating the packaging and publishing of Helm charts to a Harbor registry via Azure DevOps Pipelines.

## Decisions

### 1. Pipeline Trigger
**Decision**: Trigger on `main` branch pushes and PRs affecting `charts/**`.
**Rationale**: Ensures charts are packaged and tested on every relevant change, but only published from `main` (controlled via conditional steps).
**Alternatives considered**: 
- Scheduled triggers (rejected: want immediate feedback).
- Tag-based triggers (rejected: want continuous delivery for latest).

### 2. Authentication Mechanism
**Decision**: Use `helm registry login` with credentials provided via Azure DevOps Library (Secret Variables).
**Rationale**: Simple, standard OCI authentication supported by Helm 3.8+.
**Alternatives considered**: 
- Docker task (rejected: Helm CLI is more direct for chart operations).
- Service Connection (rejected: straightforward env vars are easier for generic script tasks if Service Connection type doesn't natively support Helm OCI push easily). *Refinement: Will try to use standard Helm task if available, otherwise script.*

### 3. Versioning Strategy
**Decision**: Extract version from `Chart.yaml`.
**Rationale**: Single source of truth.
**Alternatives considered**: 
- Build ID (rejected: semantic versioning is preferred for charts).

## Technology Best Practices
- **Helm**: Use `helm package` and `helm push` (OCI support).
- **Azure DevOps**: Use `azure-pipelines.yml` with multi-stage or conditional steps.
