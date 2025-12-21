---

description: "Task list for GitHub Actions Docker Image Build and Push feature"
---

# Tasks: GitHub Actions Docker Image Build and Push

**Input**: Design documents from `/specs/001-github-actions-docker-push/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are primarily verification steps for this CI/CD feature.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the repository for the new workflow.

- [x] T001 Verify Dockerfile existence and validity at repository root
- [x] T002 [P] Create .github/workflows directory if it doesn't exist

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core configuration that MUST be complete before user stories can operate.

- [x] T003 Ensure GITHUB_TOKEN permissions are set in repository settings (if not default)
- [x] T004 [P] Review existing docker-publish.yml for potential conflicts

**Checkpoint**: Repository is ready for workflow definition.

---

## Phase 3: User Story 1 - Automated Image Delivery (Priority: P1) 🎯 MVP

**Goal**: Automatically build and push Docker images to GHCR on commits to `main`.

**Independent Test**: Push to `main` triggers workflow; image appears in GHCR with `latest` and `sha-HASH` tags.

### Implementation for User Story 1

- [x] T005 [US1] Define workflow triggers for `push` to `main` in .github/workflows/docker-publish.yml
- [x] T006 [US1] Configure `docker/setup-buildx-action` and `docker/login-action` in .github/workflows/docker-publish.yml
- [x] T007 [US1] Configure `docker/metadata-action` for tagging (`latest`, `sha`, `semver`) in .github/workflows/docker-publish.yml
- [x] T008 [US1] Configure `docker/build-push-action` to build and push to GHCR in .github/workflows/docker-publish.yml
- [x] T009 [US1] Validate workflow syntax (dry-run or linter)

**Checkpoint**: Automated builds on `main` are functional.

---

## Phase 4: User Story 2 - Manual Workflow Trigger (Priority: P2)

**Goal**: Allow manual triggering of the workflow from GitHub Actions UI.

**Independent Test**: "Run workflow" button appears in Actions tab; clicking it triggers a successful build/push.

### Implementation for User Story 2

- [x] T010 [US2] Add `workflow_dispatch` trigger to .github/workflows/docker-publish.yml
- [x] T011 [US2] (Optional) Add input parameters for manual trigger (e.g., custom tags) in .github/workflows/docker-publish.yml

**Checkpoint**: Manual trigger is available and functional.

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect the workflow.

- [x] T012 Update README.md to badge/reference the new workflow and image location
- [x] T013 Verify image metadata labels (org.opencontainers.image.*)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Setup.
- **User Story 1 (P1)**: Depends on Foundational.
- **User Story 2 (P2)**: Can be implemented alongside or after US1 (modifies same file, but logical extension).
- **Polish**: Depends on US1 completion.

### Parallel Opportunities

- T001 and T002 can run in parallel.
- T003 and T004 can run in parallel.
- T012 can be drafted while workflow is being implemented.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup & Foundational.
2. Implement US1 (Automated Build/Push).
3. Validate by pushing to a feature branch (with modified trigger for testing) or merging to main.

### Incremental Delivery

1. Deliver US1 (Automated).
2. Add US2 (Manual Trigger) as an enhancement.
