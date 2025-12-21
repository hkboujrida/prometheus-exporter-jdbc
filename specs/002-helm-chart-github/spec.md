# Feature Specification: Helm Chart Creation & Publishing

**Feature Branch**: `002-helm-chart-github`  
**Created**: 2025-12-21  
**Status**: Draft  
**Input**: User description: "create a push a helm chart into github"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Helm Chart Creation (Priority: P1)

As a Kubernetes Administrator, I want a standard Helm chart for the Prometheus JDBC Exporter so that I can deploy and manage the application using industry-standard package management tools rather than raw manifests.

**Why this priority**: Simplifies deployment and configuration management for end users.

**Independent Test**: Can be verified by running `helm install` using the generated chart and confirming the application starts correctly in a Kubernetes cluster.

**Acceptance Scenarios**:

1. **Given** the existing raw Kubernetes manifests, **When** the Helm chart is created, **Then** it should template key configuration values (image, port, config.json settings).
2. **Given** a default values.yaml, **When** `helm install` is run, **Then** the application deploys with sensible defaults.

---

### User Story 2 - Automated Chart Publishing (Priority: P1)

As a DevOps Engineer, I want the Helm chart to be automatically packaged and published to a Helm repository hosted on GitHub whenever changes are pushed to the main branch.

**Why this priority**: Automates the distribution of the chart, ensuring users always have access to the latest version.

**Independent Test**: Push a change to the chart source, verify a new version appears in the GitHub Pages hosting the repository.

**Acceptance Scenarios**:

1. **Given** a change to the Helm chart source, **When** pushed to main, **Then** a GitHub Action packages the chart.
2. **Given** a packaged chart, **When** the action completes, **Then** the repository index.yaml is updated and the chart tarball is accessible via GitHub Pages.

### Edge Cases

- **Invalid Chart**: The workflow MUST fail if `helm lint` fails.
- **Version Conflict**: The workflow should handle scenarios where the chart version is not incremented (fail or skip).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a Helm chart that replicates the functionality of the existing `k8s/` manifests.
- **FR-002**: The Helm chart MUST expose configuration options for the `config.json` content via `values.yaml`.
- **FR-003**: The system MUST automate the packaging and publishing of the Helm chart using GitHub Actions.
- **FR-004**: The Helm repository MUST be hosted on GitHub Container Registry (OCI).
- **FR-005**: The automated workflow MUST lint the chart before publishing.

### Key Entities

- **Helm Chart**: A collection of files that describe a related set of Kubernetes resources.
- **Helm Repository**: An HTTP server that houses an `index.yaml` file and chart packages.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can install the exporter using `helm repo add <repo> && helm install <release> <chart>`.
- **SC-002**: A new chart version is published within 5 minutes of a merge to main affecting the chart directory.
- **SC-003**: The Helm chart passes standard `helm lint` checks.