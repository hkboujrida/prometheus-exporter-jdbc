---

description: "Task list for Helm Chart Creation & Publishing feature"
---

# Tasks: Helm Chart Creation & Publishing

**Input**: Design documents from `/specs/002-helm-chart-github/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are verification steps (helm lint, install) as part of the tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the repository for Helm chart development.

- [x] T001 Create `charts/prometheus-exporter-jdbc` directory structure
- [x] T002 Initialize standard Helm chart template (Chart.yaml, .helmignore) in `charts/prometheus-exporter-jdbc/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core chart components that must exist before functional testing.

- [x] T003 Create `charts/prometheus-exporter-jdbc/values.yaml` matching contract definition
- [x] T004 Create `charts/prometheus-exporter-jdbc/templates/_helpers.tpl` for standard labels and names
- [x] T005 Create `charts/prometheus-exporter-jdbc/templates/serviceaccount.yaml` (optional but recommended standard)

**Checkpoint**: Chart structure is valid and `helm lint` passes (with empty templates).

---

## Phase 3: User Story 1 - Helm Chart Creation (Priority: P1)

**Goal**: A functional Helm chart that deploys the exporter.

**Independent Test**: `helm install test-release ./charts/prometheus-exporter-jdbc` deploys a running pod.

### Implementation for User Story 1

- [x] T006 [US1] Create `charts/prometheus-exporter-jdbc/templates/configmap.yaml` to serialize `config` value to `config.json`
- [x] T007 [US1] Create `charts/prometheus-exporter-jdbc/templates/service.yaml` exposing the exporter port
- [x] T008 [US1] Create `charts/prometheus-exporter-jdbc/templates/deployment.yaml` mounting the ConfigMap and using image values
- [x] T009 [US1] Verify chart with `helm lint` and `helm template` (dry-run)
- [x] T010 [US1] Document installation instructions in `charts/prometheus-exporter-jdbc/README.md`

**Checkpoint**: Helm chart is locally installable and functional.

---

## Phase 4: User Story 2 - Automated Chart Publishing (Priority: P1)

**Goal**: Automated packaging and pushing of the Helm chart to GHCR (OCI).

**Independent Test**: Push change to `charts/` -> Action runs -> `helm pull oci://...` works.

### Implementation for User Story 2

- [x] T011 [US2] Create `.github/workflows/helm-publish.yml` with OCI push logic
- [x] T012 [US2] Configure `helm lint` step in the workflow
- [x] T013 [US2] Configure `helm package` step in the workflow
- [x] T014 [US2] Configure `helm push` step using `GITHUB_TOKEN` authentication in the workflow

**Checkpoint**: Chart updates on main branch are automatically published.

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and final cleanup.

- [x] T015 Update root `README.md` with Helm installation instructions (referencing `quickstart.md`)
- [x] T016 Verify `.helmignore` excludes unnecessary files (like `.git`, `tests/`)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Setup.
- **User Story 1 (P1)**: Depends on Foundational.
- **User Story 2 (P1)**: Depends on User Story 1 (chart must exist to be published).
- **Polish**: Depends on US2 completion.

### Parallel Opportunities

- T006, T007, T008 (Templates) can be drafted in parallel if `_helpers.tpl` is agreed upon.
- T011 (Workflow) can be drafted while the chart is being built (US1), but testing requires the chart to exist.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup & Foundational.
2. Implement US1 (Chart creation).
3. Validate locally with `minikube` or `kind` if available, or just `helm template`.

### Incremental Delivery

1. Deliver US1 (Chart).
2. Add US2 (Automation) to start publishing artifacts.
