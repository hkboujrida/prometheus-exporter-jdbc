# Quickstart: Using the Helm Chart

## Prerequisites
- Kubernetes cluster
- Helm v3+ installed

## Installation

### 1. Login to Registry
Since the chart is stored in GHCR (OCI), you may need to authenticate:

```bash
helm registry login ghcr.io -u <username> -p <token>
```

### 2. Install the Chart

```bash
helm install my-exporter oci://ghcr.io/hkboujrida/charts/prometheus-exporter-jdbc --version <version>
```

### 3. Custom Configuration

Create a `my-values.yaml` file to override defaults:

```yaml
config:
  queries:
    - name: "My Custom Metric"
      sql: "SELECT COUNT(*) FROM MY_TABLE"
      interval: 30
```

Install with custom values:

```bash
helm install my-exporter oci://ghcr.io/hkboujrida/charts/prometheus-exporter-jdbc --values my-values.yaml
```

## Verification

Check the pods and logs:

```bash
kubectl get pods
kubectl logs -l app.kubernetes.io/name=prometheus-exporter-jdbc
```
