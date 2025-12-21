<!--
Sync Impact Report:
- Version Change: Template -> 1.0.0
- New Principles:
  - I. Configuration-First (Defined)
  - II. Universal Connectivity (Defined)
  - III. Container-Native (Defined)
  - IV. Observability (Defined)
  - V. Stability & Compatibility (Defined)
- Added Sections: Security Standards, Development & Release, Governance
- Templates Status:
  - .specify/templates/plan-template.md: ✅
  - .specify/templates/spec-template.md: ✅
  - .specify/templates/tasks-template.md: ✅
-->
# JDBC Prometheus Exporter Constitution

## Core Principles

### I. Configuration-First
Behavior is strictly driven by external configuration (e.g., `config.json`, env vars). Defaults must be sensible, but users MUST have granular control over queries and connections without code changes.

### II. Universal Connectivity
While optimized for IBM i, the core MUST remain JDBC-agnostic. Support for arbitrary JDBC drivers via classpath and configuration is mandatory.

### III. Container-Native
The application MUST function seamlessly in containerized environments (Docker/K8s). Configuration via Environment Variables takes precedence over files where applicable. Logs MUST go to stdout/stderr.

### IV. Observability
The exporter MUST provide insights into its own operation (e.g., successful scrapes, query timings, connection errors). Failures should be visible in logs and, where appropriate, as metrics.

### V. Stability & Compatibility
Changes MUST NOT break existing `config.json` schemas or Prometheus scrape targets without a Major version bump. Deprecation warnings are required before removal.

## Security Standards

Secrets (passwords) MUST NOT be logged or hardcoded. Support for encrypted connections (SSL/TLS) via JDBC driver properties is required. Docker images MUST run as non-root users where possible.

## Development & Release

- **Build System**: Maven is the authoritative build tool.
- **Versioning**: Semantic Versioning (Major.Minor.Patch) is strictly enforced.
- **Testing**: Unit tests for logic, Integration tests for JDBC interactions (mocked or containerized DBs).
- **CI/CD**: GitHub Actions automate build, test, and container publishing.

## Governance

Amendments to this constitution require a Pull Request with "Constitution" label. Changes to Core Principles require consensus from maintainers. Adherence to these principles is checked during Code Review.

**Version**: 1.0.0 | **Ratified**: 2025-12-21 | **Last Amended**: 2025-12-21