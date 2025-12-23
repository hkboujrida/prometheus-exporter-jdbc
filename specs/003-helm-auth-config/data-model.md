# Data Model: Helm Configuration Schema

## Configuration Objects

### 1. Environment Variables (`env`)
Configuration for non-sensitive connection parameters.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `hostname` | String | `""` | Database hostname or IP address. Maps to `HOSTNAME` env var. |
| `username` | String | `""` | Database username. Maps to `USERNAME` env var. |

### 2. Secret Management (`secret`)
Configuration for handling the database password.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `create` | Boolean | `false` | If `true`, the chart creates a Secret resource containing the `password`. |
| `name` | String | `""` | Name of the Secret to use (or create). |
| `key` | String | `"password"` | Key within the Secret that contains the password. |
| `password` | String | `""` | The actual password string. ONLY used if `create` is `true`. |

## Validations

- If `env.hostname` is provided, it must be a non-empty string.
- If `env.username` is provided, it must be a non-empty string.
- If `secret.create` is `true`:
  - `secret.name` SHOULD be provided (defaults to fullname if empty).
  - `secret.password` MUST be provided.
- If `secret.create` is `false`:
  - `secret.name` MUST be provided to reference an existing secret.

## Relationship to Kubernetes Resources

- **Deployment**:
  - `env.hostname` -> `spec.template.spec.containers[0].env[name=HOSTNAME].value`
  - `env.username` -> `spec.template.spec.containers[0].env[name=USERNAME].value`
  - `secret` -> `spec.template.spec.containers[0].env[name=PASSWORD].valueFrom.secretKeyRef`

- **Secret** (Conditional):
  - Created if `secret.create` is true.
  - `metadata.name` = `secret.name` (or generated name).
  - `data[secret.key]` = base64(`secret.password`).