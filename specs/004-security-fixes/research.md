# Research: Address Security Concerns

**Context**: Improving security posture of the Prometheus Exporter JDBC Helm chart to meet Constitution standards and remediate Trivy findings.

## Decisions

### 1. Pod Security Context
**Decision**: Use strict security context:
- `runAsNonRoot: true`
- `runAsUser: 10001` (arbitrary non-root ID)
- `runAsGroup: 10001`
- `readOnlyRootFilesystem: true`
- `allowPrivilegeEscalation: false`
- `capabilities: { drop: ["ALL"] }`

**Rationale**:
- Remediation of Trivy findings.
- Application analysis confirms `config.json` is pre-baked into the image, preventing runtime write attempts in default mode.
- Non-root execution is a best practice.

**Alternatives**:
- `runAsUser: 1000` (often used, but 10001 is safer to avoid collisions with host users).
- Writable root FS (rejected: unnecessary attack surface).

### 2. Resource Limits
**Decision**: Set default resource requests/limits in `values.yaml`:
- Requests: CPU 100m, Memory 256Mi
- Limits: CPU 500m, Memory 512Mi

**Rationale**:
- Java applications require sufficient heap.
- Prevents noisy neighbor issues.
- Ensures scheduling stability.

**Alternatives**:
- No limits (rejected: allows unbounded consumption).
- Tighter limits (rejected: risk of OOMKilled for Java app).

### 3. Temporary Files
**Decision**: No `emptyDir` volume for `/tmp` initially.
**Rationale**: Code analysis did not show usage of `java.io.tmpdir` or explicit temp file creation in the main path. If issues arise, it can be added later.
