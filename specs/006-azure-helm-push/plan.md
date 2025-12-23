# Implementation Plan: Azure DevOps Helm Chart Push to Harbor

**Branch**: `006-azure-helm-push` | **Date**: 2025-12-23 | **Spec**: [specs/006-azure-helm-push/spec.md](specs/006-azure-helm-push/spec.md)
**Input**: Feature specification from `/specs/006-azure-helm-push/spec.md`

## Summary

Implement an Azure DevOps pipeline to automatically lint, package, and push the Helm chart to a Harbor OCI registry upon changes to the chart directory.

## Technical Context

**Language/Version**: YAML (Azure Pipelines), Helm v3+
**Primary Dependencies**: Azure DevOps, Harbor
**Storage**: Harbor OCI Registry
**Testing**: Manual pipeline execution verification
**Target Platform**: Azure DevOps Agents (Ubuntu)
**Project Type**: CI/CD Configuration
**Performance Goals**: N/A
**Constraints**: Requires Azure DevOps Variable Group for secrets.
**Scale/Scope**: 1 Pipeline file.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Configuration-First**: ✅ Uses Variable Groups for credentials.
- **Universal Connectivity**: N/A
- **Container-Native**: ✅ Supports OCI artifacts.
- **Observability**: ✅ Pipeline logs provide visibility.
- **Stability & Compatibility**: ✅ Versioning driven by `Chart.yaml`.
- **Security Standards**: ✅ Secrets managed via Variable Groups, not hardcoded.

## Project Structure

### Documentation (this feature)

```text
specs/006-azure-helm-push/
├── plan.md              # This file
├── research.md          # Decisions
├── data-model.md        # Variables
├── quickstart.md        # Setup guide
├── contracts/           # Pipeline YAML snippet
└── tasks.md             # To be generated
```

### Source Code (repository root)

```text
azure-pipelines-helm.yml # New pipeline definition
```

**Structure Decision**: Standard Azure DevOps YAML pipeline at root.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | | |