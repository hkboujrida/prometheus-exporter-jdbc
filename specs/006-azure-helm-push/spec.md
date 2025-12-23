# Feature Specification: Azure DevOps Helm Chart Push to Harbor

**Feature Branch**: `006-azure-helm-push`  
**Created**: 2025-12-23  
**Status**: Draft  
**Input**: User description: "build an azure devops pipelines to push helm charts to harbor"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - CI Pipeline Helm Packaging (Priority: P1)

As a DevOps engineer, I want an Azure DevOps pipeline that automatically packages the Helm chart whenever changes are made to the `charts/` directory, so that the chart artifact is ready for distribution.

**Why this priority**: Packaging is the prerequisite step for publishing.

**Independent Test**: Trigger the pipeline with a change in `charts/prometheus-exporter-jdbc/Chart.yaml` (e.g., version bump) and verify a `.tgz` file is created.

**Acceptance Scenarios**:

1. **Given** a commit with changes to the Helm chart, **When** the pipeline runs, **Then** the `helm package` command executes successfully.
2. **Given** the packaging succeeds, **Then** a chart archive (e.g., `prometheus-exporter-jdbc-0.2.1.tgz`) is available as a pipeline artifact.

---

### User Story 2 - Authenticated Push to Harbor (Priority: P1)

As a DevOps engineer, I want the pipeline to authenticate with the Harbor registry and push the packaged Helm chart, so that the chart is available for deployment by other teams/systems.

**Why this priority**: Essential for distributing the chart.

**Independent Test**: Configure a mock or test Harbor registry (or verify against logs showing successful 201 Created / pushed status).

**Acceptance Scenarios**:

1. **Given** a packaged Helm chart, **When** the pipeline attempts to push, **Then** it authenticates using provided Service Connection or credentials (env vars).
2. **Given** successful authentication, **When** `helm push` (or `helm registry push`) is called, **Then** the chart is uploaded to the specified Harbor project.
3. **Given** the push is successful, **Then** the chart appears in the Harbor repository with the correct version.

---

### User Story 3 - Versioning Strategy (Priority: P2)

As a maintainer, I want the pipeline to respect the version defined in `Chart.yaml` (or override it based on build metadata) so that unique semantic versions are published for every release.

**Why this priority**: Prevents overwriting existing chart versions and ensures traceability.

**Independent Test**: Commit `Chart.yaml` with version `1.0.0`, run pipeline. Commit again with `1.0.1`, run pipeline. Verify both versions exist in Harbor.

**Acceptance Scenarios**:

1. **Given** `Chart.yaml` has version `X.Y.Z`, **When** the pipeline runs, **Then** the pushed artifact is tagged `X.Y.Z`.
2. **Given** an existing chart with version `X.Y.Z` in Harbor, **When** the pipeline tries to push the SAME version (if immutable), **Then** the pipeline fails or skips (depending on policy). Assumption: Pipeline should fail if version conflict exists to enforce version bumping.

### Edge Cases

- **Authentication Failure**: Pipeline should fail immediately if login to Harbor fails.
- **Harbor Unavailability**: If Harbor is unreachable, the pipeline step MUST fail with a non-zero exit code and log the connection error (without exposing secrets), causing the pipeline run to fail. Retry logic is handled by re-running the pipeline manually.
- **Invalid Chart**: `helm lint` should run before packaging; if lint fails, stop pipeline.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system (Azure DevOps) MUST trigger a pipeline execution on pushes to the `main` branch or when a Pull Request affects the `charts/` directory.
- **FR-002**: The pipeline MUST allow defining Harbor registry URL, Username, and Password (secret) via Azure DevOps Library or Service Connection.
- **FR-003**: The pipeline MUST execute `helm lint` on the chart before packaging.
- **FR-004**: The pipeline MUST package the chart using `helm package`.
- **FR-005**: The pipeline MUST authenticate to the Harbor OCI registry using `helm registry login`.
- **FR-006**: The pipeline MUST push the chart artifact to the Harbor registry using `helm push`.

### Key Entities

- **Pipeline**: The definition (YAML) in Azure DevOps.
- **Harbor Registry**: The destination OCI registry.
- **Chart Artifact**: The `.tgz` file produced.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A push to the repository results in a new Helm chart version visible in the Harbor UI within 5 minutes.
- **SC-002**: The pipeline run log shows explicit "Pushed: <registry>/<chart>:<version>" success message.
- **SC-003**: Pipeline success rate > 95% for valid chart changes.