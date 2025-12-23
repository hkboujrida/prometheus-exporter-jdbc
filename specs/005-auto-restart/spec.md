# Feature Specification: Implement Auto-Restart Mechanism

**Feature Branch**: `005-auto-restart`  
**Created**: 2025-12-23  
**Status**: Draft  
**Input**: User description: "when there is an error the application does not restart: implement auto-restart mechanism. Ensure graceful shutdown and error logging. Verify restart on failure."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Exit on Critical Error (Priority: P1)

As an operator, I want the application to crash (exit with non-zero code) immediately when a critical error occurs (e.g., invalid configuration, unrecoverable database connection failure at startup) so that Kubernetes/Docker can detect the failure and restart the container.

**Why this priority**: Currently, the application may be hanging or swallowing errors, preventing self-healing.

**Independent Test**:
1. Configure the application with invalid credentials or a non-existent database URL.
2. Run the application.
3. Verify the process exits with a non-zero status code (e.g., 1).
4. Verify the error is logged to stderr.

**Acceptance Scenarios**:

1. **Given** invalid database configuration, **When** the application starts, **Then** it logs the error and exits with code 1.
2. **Given** the application is running, **When** a critical unrecoverable error occurs (e.g. main thread crash), **Then** the process terminates.

---

### User Story 2 - Liveness Probe Configuration (Priority: P2)

As an operator, I want Kubernetes to automatically restart the application if it becomes unresponsive (hangs) even if it hasn't crashed, to ensure continuous availability.

**Why this priority**: To handle cases where the app is "zombie" (running but not working).

**Independent Test**:
1. Deploy the Helm chart.
2. Verify the Pod has a `livenessProbe` configured.
3. Simulate a hang (if possible, or just verify probe success under normal load).

**Acceptance Scenarios**:

1. **Given** the Helm chart is deployed, **When** I inspect the Pod spec, **Then** a `livenessProbe` is defined pointing to a valid endpoint (e.g. TCP port open or HTTP /health).
2. **Given** the application is running normally, **When** the liveness probe executes, **Then** it succeeds.

---

### User Story 3 - Graceful Shutdown (Priority: P2)

As an operator, I want the application to clean up resources (database connections, sockets) when it receives a termination signal (SIGTERM) so that restarts are clean and don't leave zombie connections on the database.

**Why this priority**: Good citizenship in a container environment.

**Independent Test**:
1. Start the application.
2. Send `SIGTERM` (kill -15) to the process.
3. Verify logs show "Shutting down..." or similar.
4. Verify the process exits within a reasonable timeout.

**Acceptance Scenarios**:

1. **Given** the application is running, **When** it receives SIGTERM, **Then** it logs a shutdown message, closes DB connections, and exits.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST exit with a non-zero status code if initialization fails (config error, initial DB connection failure).
- **FR-002**: The application MUST log all critical errors to `stderr` with a timestamp and severity.
- **FR-003**: The application MUST register a JVM Shutdown Hook to handle graceful termination.
- **FR-004**: The Helm Chart Deployment MUST be configured with a `livenessProbe`.
- **FR-005**: The Helm Chart Deployment MUST be configured with a `readinessProbe` (optional but good practice with liveness).

### Key Entities

- **MainApp**: Entry point, needs exception handling logic.
- **Helm Deployment**: Needs probe configuration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Application exits with code != 0 on startup failure (verified by test).
- **SC-002**: K8s Pod restarts automatically when liveness probe fails (or app crashes).
- **SC-003**: Logs contain clear stack traces for failures.
- **SC-004**: Deployment YAML contains `livenessProbe` definition.

## Assumptions

- The application exposes an HTTP port (9853) that can be used for liveness/readiness probes (e.g. simple TCP check or HTTP endpoint).