# Data Model: Azure DevOps Helm Push

## Pipeline Configuration (Variables)

| Variable | Description | Source |
|----------|-------------|--------|
| `HARBOR_URL` | URL of the Harbor registry (e.g., `registry.example.com`) | Library / Env |
| `HARBOR_USERNAME` | Username for Harbor push | Library (Secret) |
| `HARBOR_PASSWORD` | Password for Harbor push | Library (Secret) |
| `CHART_PATH` | Path to chart directory | `charts/prometheus-exporter-jdbc` |

## Artifacts

### Helm Chart Package
- **Format**: `.tgz` archive (e.g., `prometheus-exporter-jdbc-1.0.0.tgz`)
- **Metadata**: Contains `Chart.yaml`, `values.yaml`, templates.
- **Destination**: Harbor OCI Registry project.
