# Data Model: Helm Chart

**Feature**: Helm Chart Creation & Publishing

## 1. Artifact Structure

The Helm chart acts as a package for the application deployment.

**Package Name**: `prometheus-exporter-jdbc`
**Registry**: `oci://ghcr.io/<owner>/charts`

## 2. Configuration Model (values.yaml)

The `values.yaml` file serves as the data interface for the user.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `image.repository` | string | `ghcr.io/<owner>/prometheus-exporter-jdbc` | Docker image repo |
| `image.tag` | string | `latest` (or specific version) | Docker image tag |
| `service.port` | integer | `9853` | Service port |
| `config` | string/object | `{}` (Default config content) | Content of `config.json` |
| `securityContext` | object | `{ runAsNonRoot: true }` | Security settings |

## 3. Workflow Data

**Inputs**:
- Source code changes in `charts/`

**Outputs**:
- OCI Artifact in GHCR (`application/vnd.cncf.helm.config.v1+json`)
