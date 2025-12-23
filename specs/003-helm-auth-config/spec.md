# Feature Specification: Enable configuration of username, hostname, and password secret in Helm chart

**Feature Branch**: `003-helm-auth-config`  
**Created**: 2025-12-22  
**Status**: Draft  
**Input**: User description: "in the helm chart i need to specify the username the hostname as env variables and create the user password secret and provide its reference to the helm chart to be loaded in the pod as env as a secret"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configure Hostname and Username (Priority: P1)

As a DevOps engineer deploying the application, I want to specify the database hostname and username directly in the Helm values file so that the application connects to the correct database instance without requiring manual image rebuilds or manual manifest edits.

**Why this priority**: Essential for the application to function in different environments (dev, stage, prod).

**Independent Test**: Deploy the chart with custom hostname/username values and verify the pod environment variables match.

**Acceptance Scenarios**:

1. **Given** a Helm values file with `env.hostname` set to "db-prod.example.com" and `env.username` set to "admin_user", **When** the chart is installed, **Then** the created Pod has an environment variable `HOSTNAME` with value "db-prod.example.com" and `USERNAME` with value "admin_user".
2. **Given** no values provided for hostname/username, **When** the chart is installed, **Then** the Pod uses default values or remains unset (depending on default `values.yaml` configuration).

---

### User Story 2 - Secure Password Injection via External Secret (Priority: P1)

As a security-conscious engineer, I want to manage the database password in a Kubernetes Secret (created externally or by a secrets manager) and reference this secret in the Helm chart, so that the password is injected into the application securely as an environment variable.

**Why this priority**: Critical for security compliance; passwords should not be hardcoded in plain text values files.

**Independent Test**: Create a dummy secret, deploy the chart referencing it, and verify the pod's `PASSWORD` env var is populated from the secret.

**Acceptance Scenarios**:

1. **Given** an existing Kubernetes Secret named `my-db-secret` containing key `db-password`, **When** I configure the Helm chart to use this secret name and key, **Then** the Pod has an environment variable `PASSWORD` whose value is populated from the `my-db-secret` data.

---

### User Story 3 - Chart-Managed Secret Creation (Priority: P2)

As a developer setting up a quick test environment, I want the option to provide the password directly in `values.yaml` and have the Helm chart create the Secret for me, so that I don't have to manually run `kubectl create secret` commands.

**Why this priority**: Improves developer experience and ease of use for non-production setups.

**Independent Test**: Deploy chart with a password string in values and `createSecret: true`, verify a Secret resource is created and the Pod links to it.

**Acceptance Scenarios**:

1. **Given** the Helm values specify a password string "mySuperSecret" and `secret.create` is true, **When** the chart is installed, **Then** a Kubernetes Secret is created containing the password.
2. **Given** the above scenario, **Then** the Pod is configured to read the `PASSWORD` environment variable from this newly created Secret.

### Edge Cases

- **Missing Secret**: If the referenced secret does not exist, the Pod will fail to start (ContainerConfigError) - this is expected K8s behavior.
- **Conflict**: If `secret.create` is true AND `secret.name` is provided, the chart will attempt to create a Secret with the provided name. If a Secret with that name already exists and is managed by Helm, the upgrade may succeed or fail depending on Helm's ownership checks. If it exists and is unmanaged, the install will fail. **Decision**: User is responsible for ensuring name uniqueness or managing the conflict.
- **Empty Values**: If hostname/username are empty strings, the application might fail to connect or prompt for input (in interactive mode). In a pod, it should likely fail or use defaults.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Helm chart MUST allow configuration of `hostname` and `username` via `values.yaml`.
- **FR-002**: The Deployment template MUST inject `HOSTNAME` and `USERNAME` environment variables into the container based on the configured values.
- **FR-003**: The Helm chart MUST allow configuration of a Secret name and Secret key to be used for the `PASSWORD` environment variable.
- **FR-004**: The Deployment template MUST use `valueFrom.secretKeyRef` to inject the `PASSWORD` environment variable.
- **FR-005**: The Helm chart MUST support optionally creating a Secret resource if a password is provided in `values.yaml` and a creation flag is enabled.
- **FR-006**: If the chart creates a Secret, the Deployment MUST be configured to reference this auto-generated secret.

### Key Entities

- **Secret**: Kubernetes resource storing the password.
- **Deployment**: Kubernetes resource managing the application pod.
- **ConfigMap/EnvVars**: Mechanism to pass hostname/username.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Application successfully connects to the database using credentials provided via Helm (verified by application logs "Connected" message or absence of connection errors).
- **SC-002**: Inspection of the running Pod shows `HOSTNAME`, `USERNAME`, and `PASSWORD` environment variables are present and correct.
- **SC-003**: No sensitive password data is visible in the Pod spec `env` section (must use `valueFrom`).