# Research: GitHub Actions Docker Workflow

**Feature**: GitHub Actions Docker Image Build and Push
**Date**: 2025-12-21

## Key Decisions

### 1. Workflow Actions
**Decision**: Use the standard Docker official actions suite:
- `docker/setup-buildx-action`
- `docker/login-action`
- `docker/metadata-action`
- `docker/build-push-action`

**Rationale**: These are the industry standard, maintained by Docker, and offer the most robust support for caching, multi-platform builds, and metadata handling.

**Alternatives Considered**:
- *Shell scripts (`docker build ...`)*: Rejected because they lack built-in caching and cross-platform support is harder to manage.
- *Custom actions*: Unnecessary risk and maintenance burden.

### 2. Registry Authentication
**Decision**: Use `GITHUB_TOKEN` for authentication to GHCR.

**Rationale**: 
- **Security**: It's a temporary, automatically rotated token. No long-lived secrets to manage.
- **Simplicity**: Zero configuration required for repository administrators.
- **Constitution Compliance**: Aligns with "Secrets MUST NOT be logged or hardcoded".

**Alternatives Considered**:
- *Personal Access Token (PAT)*: Rejected. Requires manual rotation and management, introduces security risk if leaked.

### 3. Tagging Strategy
**Decision**: Use `docker/metadata-action` to generate tags automatically:
- `latest` (for main branch)
- `sha-<commit-hash>` (for traceability)
- `pr-<number>` (for pull requests, optional but good practice)
- SemVer tags (for releases, e.g., `v1.2.3`)

**Rationale**: Automated and consistent. Ensures every image can be traced back to the exact source commit (Spec SC-003).

### 4. Image Naming
**Decision**: `ghcr.io/${{ github.repository }}` (lowercased).

**Rationale**: Standard convention for GHCR.
