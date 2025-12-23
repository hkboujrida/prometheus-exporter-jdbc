# Quickstart: Auto-Restart Mechanism

## Testing Exit on Failure

1. **Modify Config**: Intentionally provide a malformed `config.json` (e.g. invalid JSON) or set a port that is already in use.
2. **Run Application**:
   ```bash
   java -jar target/prom-client-ibmi.jar
   ```
3. **Verify Status**:
   ```bash
   echo $?
   ```
   *Expected Output*: `1` (or non-zero).

## Testing Liveness Probes (Kubernetes)

1. **Deploy Chart**:
   ```bash
   helm install restart-test ./charts/prometheus-exporter-jdbc
   ```
2. **Describe Pod**:
   ```bash
   kubectl describe pod -l app.kubernetes.io/name=prometheus-exporter-jdbc
   ```
   *Expected Outcome*: `Liveness` and `Readiness` fields are populated under the container spec.
3. **Verify Logs**:
   ```bash
   kubectl logs -f <pod-name>
   ```
   Wait for startup and ensure probes are not failing (no `GET / 4xx/5xx` errors unless app is unhealthy).

## Testing Graceful Shutdown

1. **Send SIGTERM**:
   ```bash
   kill -15 <pid>
   ```
2. **Check Logs**:
   Look for "Shutting down application..." or similar success message from the Jetty server stop event.
