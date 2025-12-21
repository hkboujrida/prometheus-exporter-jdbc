# Data Model: Docker Workflow

**Feature**: GitHub Actions Docker Image Build and Push

## 1. Workflow Inputs (Triggers)

The workflow is triggered by Git events. It does not accept user data payload but acts on the repository state.

| Event | Condition | Action |
|-------|-----------|--------|
| `push` | `branches: [main]` | Build & Push `latest`, `sha-<hash>` |
| `push` | `tags: [v*]` | Build & Push `vX.Y.Z` |
| `pull_request` | `branches: [main]` | Build only (dry-run validation) |
| `workflow_dispatch` | `(manual)` | Build & Push (optional inputs) |

## 2. Secrets & Environment

The workflow relies on the following injected context:

| Name | Source | Description |
|------|--------|-------------|
| `GITHUB_TOKEN` | GitHub Actions | Used to authenticate with `ghcr.io` |
| `GITHUB_REPOSITORY` | Context | Used to derive image name |
| `GITHUB_SHA` | Context | Used for version tagging |

## 3. Artifact: Docker Image

The output is a container image stored in GHCR.

**Image Reference**: `ghcr.io/<owner>/<repo>:<tag>`

**Tagging Schema**:
- `latest`: Points to the most recent successful build on `main`.
- `sha-XXXXXXX`: Short SHA of the commit.
- `vX.Y.Z`: SemVer tag (from git tag).
