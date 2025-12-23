# Data Model: Auto-Restart Mechanism

*No changes to database schema or persistent entities.*

## Application Behavior Entities

### Main Thread State
- **Transitions**: 
  - `INITIALIZING` -> `RUNNING`: Successful startup.
  - `INITIALIZING` -> `CRASHED`: Configuration error or port conflict. Calls `System.exit(1)`.
  - `RUNNING` -> `SHUTTING_DOWN`: Received `SIGTERM`. Triggers Shutdown Hook.
  - `RUNNING` -> `CRASHED`: Unexpected runtime exception. Calls `System.exit(1)`.

## Configuration Entities (Helm)

### Liveness Probe Spec
| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `path` | string | `/` | Endpoint for health check |
| `port` | number | `9853` | Port for health check |
| `initialDelaySeconds` | number | `30` | Delay before first probe |
| `periodSeconds` | number | `10` | Interval between probes |

### Readiness Probe Spec
| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `path` | string | `/` | Endpoint for readiness check |
| `port` | number | `9853` | Port for readiness check |
| `initialDelaySeconds` | number | `5` | Delay before first probe |
| `periodSeconds` | number | `10` | Interval between probes |
