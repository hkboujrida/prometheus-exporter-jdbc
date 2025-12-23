# Research: Implement Auto-Restart Mechanism

**Context**: The application currently exits with status 0 even on critical errors, preventing Kubernetes from automatically restarting the container. Additionally, there is no liveness probe to detect hangs.

## Decisions

### 1. Exit Code on Error
**Decision**: Update `MainApp.java` to explicitly call `System.exit(1)` when an unrecoverable exception is caught in the main thread.
**Rationale**: Kubernetes considers status 0 as a successful completion. Status 1 (or any non-zero) triggers the restart policy.
**Alternatives considered**: 
- Using a wrapper script: Rejected as it adds complexity and is less reliable than native JVM exit.

### 2. Liveness and Readiness Probes
**Decision**: Implement `livenessProbe` and `readinessProbe` in the Helm chart using the application's metric port (9853).
**Rationale**: The application already exposes a metric endpoint. A successful scrape indicates the server is alive and responding. 
- Path: `/` (or specific metric path if preferred, `/` works for basic check).
- Type: `httpGet`.
**Alternatives considered**: 
- `tcpSocket`: Simpler but doesn't verify if the application layer is actually healthy.

### 3. Graceful Shutdown
**Decision**: Add a JVM Shutdown Hook in `MainApp.java` to log the shutdown event and gracefully stop the Jetty server.
**Rationale**: Ensures that database connections are closed and logs are flushed before the process terminates.
**Alternatives considered**: 
- `PreStop` hook in Kubernetes: Native JVM hook is more portable across different container runtimes.

## Technology Best Practices
- **Java**: Use `try-with-resources` for connection management where possible. Ensure `MainApp` catches `Throwable` at the top level to prevent silent exits.
- **Kubernetes**: Set `initialDelaySeconds` for probes to allow the Java application (which has a slow startup) to fully initialize.
