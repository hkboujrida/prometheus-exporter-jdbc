# Quickstart: Testing Security Fixes

## Prerequisites
- Minikube or Kind cluster
- Helm v3 installed
- Trivy installed

## Manual Verification

1. **Deploy Chart**:
   ```bash
   helm install secure-test charts/prometheus-exporter-jdbc --set image.pullPolicy=IfNotPresent
   ```

2. **Verify Pod Status**:
   ```bash
   kubectl get pods
   # Status should be Running
   ```

3. **Verify Security Context**:
   ```bash
   kubectl get pod -l app.kubernetes.io/name=prometheus-exporter-jdbc -o jsonpath='{.items[0].spec.containers[0].securityContext}'
   # Should show: {"allowPrivilegeEscalation":false,"capabilities":{"drop":["ALL"]},"readOnlyRootFilesystem":true}
   
   kubectl get pod -l app.kubernetes.io/name=prometheus-exporter-jdbc -o jsonpath='{.items[0].spec.securityContext}'
   # Should show: {"runAsGroup":10001,"runAsNonRoot":true,"runAsUser":10001}
   ```

4. **Test Read-Only Filesystem**:
   ```bash
   kubectl exec -it <pod-name> -- touch /test-file
   # Should fail: "touch: /test-file: Read-only file system"
   ```

5. **Test Trivy Scan**:
   ```bash
   trivy config charts/prometheus-exporter-jdbc
   # Should show significantly fewer HIGH/MEDIUM issues
   ```
