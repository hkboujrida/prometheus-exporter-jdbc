# prometheus-exporter-jdbc Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-10-05

## Current Stack

- .NET 10 (ASP.NET Core minimal API + Kestrel), C#
- prometheus-net (metrics exposition), System.Data.Common providers
  (built-in: ODBC via IBM i Access driver, SQLite; custom ADO.NET providers
  loaded dynamically via `driver_assembly`/`driver_factory`)
- Docker (multi-stage, `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`)
- Helm v3+, YAML + Kubernetes 1.19+ (Pod Security Context, read-only filesystem)
- GitHub Actions (`.github/workflows/dotnet-ci.yml`, `docker-publish.yml`,
  `build-release.yml`, `helm-publish.yml`); Azure Pipelines (Docker → Harbor)
- xUnit for unit + SQLite integration tests; Docker compose harnesses under
  `tests/e2e/` for Prometheus e2e and Java-parity output diffing

## Project Structure

```text
PromExporter.slnx
src/PromExporter.Jdbc/        # exporter (Program.cs, ExporterConfig, QueryCollector,
                              # DbConnectionProvider, GaugeNaming, Resources/)
tests/PromExporter.Jdbc.Tests # xUnit: unit + SQLite integration
tests/e2e/                    # compose harnesses: run-e2e.sh, parity-check.sh
charts/, k8s/                 # deployment (image-agnostic, unchanged by rewrite)
specs/                        # historical feature specs (Java era)
```

## Commands

```bash
dotnet build PromExporter.slnx -c Release        # build
dotnet test  PromExporter.slnx -c Release        # unit + SQLite integration
./tests/e2e/run-e2e.sh                           # docker e2e w/ real Prometheus
./tests/e2e/parity-check.sh                      # Java<->.NET output parity gate
docker build -t prom-dotnet-local .              # image only
```

## Code Style

C#/.NET: follow standard conventions (`dotnet format`); nullable reference
types enabled. YAML/GitHub Actions: standard conventions.

## Recent Changes
- dotnet-rewrite (2026-10): application stack moved from Java 8 + Jetty +
  simpleclient to .NET 10 + ASP.NET Core + prometheus-net. Config format,
  metric names, endpoints (9853, `/metrics`, `/metrics_now`), Docker env
  contract (PORT/HOSTNAME/USERNAME/PASSWORD) unchanged; k8s manifests and
  Helm chart required no changes.
- 006-azure-helm-push: Added YAML (Azure Pipelines), Helm v3+ + Azure DevOps, Harbor
- 005-auto-restart: Java 8 + Jetty, Prometheus Client (historical, superseded)
- 004-security-fixes: Added Helm v3+, YAML, Kubernetes 1.19+ (Pod Security Context)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
