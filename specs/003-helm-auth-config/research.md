# Research: Enable configuration of username, hostname, and password secret in Helm chart

**Status**: Complete
**Date**: 2025-12-22

## Executive Summary
The Java application `prometheus-exporter-jdbc` natively supports configuration via environment variables (`HOSTNAME`, `USERNAME`, `PASSWORD`). This confirms that no code changes are required in the Java application itself. The implementation will focus entirely on modifying the Helm chart to expose these configurations in `values.yaml` and inject them into the Deployment manifest.

## Key Findings

### 1. Application Support for Environment Variables
**Findings**:
- `Config.java` checks `System.getenv("HOSTNAME")`.
- `Config.java` checks `System.getenv("USERNAME")`.
- `Config.java` checks `System.getenv("PASSWORD")`.

**Implication**: The application is already "Container-Native" compliant regarding configuration priority (Env Var > Config File).

### 2. Helm Chart Architecture
**Current State**:
- Uses a ConfigMap mounted at `/app/config.json`.
- No `env` section in `deployment.yaml`.

**Required Changes**:
- Add `env` section to `deployment.yaml`.
- Define new values structure in `values.yaml`.
- Add conditional Secret creation logic.

## Decisions

### Decision 1: Configuration Structure
**Decision**: Use a flat `env` object for plain text vars and a `secret` object for password management in `values.yaml`.

**Rationale**:
- Separates concerns: `env` for connection parameters, `secret` for sensitive data.
- Matches standard Helm patterns.

**Proposed Structure**:
```yaml
env:
  hostname: ""
  username: ""

secret:
  create: false
  name: ""
  key: "password"
  password: "" # Only used if create is true
```

### Decision 2: Secret Management
**Decision**: Support both external secrets and chart-managed secrets.

**Rationale**:
- **External**: Required for production (GitOps, Vault, SealedSecrets).
- **Chart-Managed**: Required for quick starts and dev/test environments.

## Alternatives Considered

### Alternative 1: ConfigMap Injection
Inject all values into the `config.json` via ConfigMap.
- **Rejected**: Password would be in plain text in ConfigMap (insecure). `Config.java` warns about insecure passwords in files. Env vars are preferred for secrets in this app context.

### Alternative 2: Sidecar for Secrets
Use a sidecar to read secrets and write config.
- **Rejected**: Overkill. Native K8s Secret injection is sufficient and supported by the app.