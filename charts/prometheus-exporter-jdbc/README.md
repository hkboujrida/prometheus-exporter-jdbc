# Prometheus JDBC Exporter Helm Chart

A Helm chart for deploying the Prometheus JDBC Exporter on Kubernetes.

## Prerequisites

- Kubernetes 1.19+
- Helm 3.2.0+

## Installing the Chart

To install the chart with the release name `my-release`:

```bash
# Login to GHCR (if pulling from private repo or OCI)
helm registry login ghcr.io -u <username> -p <token>

# Install from OCI registry
helm install my-release oci://ghcr.io/hkboujrida/charts/prometheus-exporter-jdbc --version <version>
```

## Configuration

The following table lists the configurable parameters of the chart and their default values.

| Parameter | Description | Default |
|-----------|-------------|---------|
| `image.repository` | Image repository | `ghcr.io/hkboujrida/prometheus-exporter-jdbc` |
| `image.tag` | Image tag | `latest` |
| `service.port` | Service port | `9853` |
| `env.hostname` | Database hostname | `""` |
| `env.username` | Database username | `""` |
| `secret.create` | Create a new secret for password | `false` |
| `secret.name` | Name of the secret to use or create | `""` |
| `secret.key` | Key in the secret containing the password | `"password"` |
| `secret.password` | Password value (used if secret.create is true) | `""` |
| `config` | Application configuration (injected as config.json) | (See values.yaml) |

Specify each parameter using the `--set key=value[,key=value]` argument to `helm install`.

## Custom Config

You can provide your own `config.json` structure via the `config` value in `values.yaml`.

```yaml
config:
  queries:
    - name: "My Custom Metric"
      sql: "SELECT COUNT(*) FROM MY_TABLE"
      interval: 30
```
