# Repository Guidelines

## Project Overview

Prometheus exporter for IBM i and other databases: reads SQL queries from a
`config.json`, executes them on interval, and exposes the numeric results as
Prometheus gauges on `/metrics`. .NET 10 rewrite (C#, ASP.NET Core minimal API
+ Kestrel + prometheus-net) of a Java/JDBC exporter; the config format, metric
names, and exposition format are deliberately Java-parity — `PLAN-dotnet-rewrite.md`
is the authoritative port spec and deviation log, `specs/001-006/` are frozen
Java-era spec-kit history (do not update them, do not treat as build docs).

## Architecture & Data Flow

Manual composition root — **no DI container usage**: everything is constructed
in `Program.Main`; `WebApplication.CreateBuilder` serves only as a Kestrel host
(`builder.Services` untouched, no prometheus-net middleware, `ClearProviders()`
is called).

```
config.json + env (PORT, PROMCLIENT_*, HOSTNAME, USERNAME, PASSWORD)
  -> ExporterConfig.Load(path, IConsoleInteraction, ILogger)
  -> per query: DbConnectionProvider (cached connection, reconnect on demand)
  -> per query: QueryCollector (owns private CollectorRegistry)
GET /  and  GET /metrics -> concatenate each collector's ExportAsync(stream)
GET /metrics_now         -> Task.WhenAll(GatherNowAsync(5000)) then scrape
```

Key invariants:
- Each `QueryCollector` owns its own `CollectorRegistry` (`Metrics.NewCustomRegistry()`),
  because prometheus-net has no public metric-removal API. On gather error the
  registry is swapped wholesale (`_gauges.Clear(); ResetRegistry();`) — that is
  the "metrics vanish while DB is down" behavior; recovery re-registers.
- Per-collector `SemaphoreSlim(1,1)` serializes gathers (providers need no
  locking); interval loop is `PeriodicTimer` in `Task.Run`, started only after
  the first successful gather; `_lastSuccessTimestamp` via `Interlocked` on
  `Environment.TickCount64`.
- Parity quirks to preserve: SQL is split on the literal `"; "` (all parts but
  the last are per-cycle setup statements); single-row mode registers gauges
  from numeric columns of the first row only; multi-row mode derives row name
  from column 1; gauge name `[host__][prefix__][row__]column` sanitized to
  `[A-Za-z0-9_]` with leading-digit underscore fix; labels `hostname`
  (dot-truncated) + `driver_class`; `/metrics_now` skips gathers within a 5 s
  tolerance window.

## Key Directories

| Path | Purpose |
|---|---|
| `src/PromExporter.Jdbc/` | the exporter: `Program.cs` (entry, endpoints, `sc` subcommand), `ExporterConfig.cs` (all config/env/prompt logic + `BuildConnectionString`), `QueryCollector.cs` (gather engine), `DbConnectionProvider.cs` (odbc/sqlite/custom provider factory, incl. `Assembly.LoadFrom` custom driver), `GaugeNaming.cs`, `Resources/` (embedded IBM i default config + Service Commander yml) |
| `tests/PromExporter.Jdbc.Tests/` | xUnit unit + SQLite integration |
| `tests/e2e/` | two Docker-compose gates (see Testing & QA) |
| `charts/prometheus-exporter-jdbc/`, `k8s/` | Helm chart + plain manifests (config via ConfigMap key `config.json` mounted at `/app/config.json`, creds via Secret env) |
| `.github/workflows/` | dotnet CI, GHCR docker publish, release zip, Helm OCI push |
| `specs/`, `.specify/` | read-only Java-era spec-kit records; `.specify/scripts/bash/update-agent-context.sh` regenerates `GEMINI.md` |

## Development Commands

```bash
dotnet build PromExporter.slnx -c Release   # solution is the new XML .slnx format
dotnet test  PromExporter.slnx -c Release   # unit + integration tiers
./tests/e2e/run-e2e.sh                      # Docker gate: real Prometheus asserts
./tests/e2e/parity-check.sh                 # Java<->.NET /metrics parity diff gate
```

Run locally: `dotnet run --project src/PromExporter.Jdbc -- --config=path/to/config.json`;
no config file + interactive TTY generates the IBM i default config.
`--verbose` enables request logging; `sc` subcommand writes a Service Commander
`prometheus.yml`.

## Code Conventions & Common Patterns

- `net10.0`, `Nullable` + `ImplicitUsings` enable, file-scoped namespaces except
  `Program.cs` (intentionally global-namespace `internal static class Program`).
  Nullable reference types clean — keep new code warning-free (CI has no
  explicit warnaserror, so review is the gate).
- Errors from a gather are logged and converted into gauge-eviction, not
  rethrown — except the *first* gather, which fails startup fast
  (`Assert.ThrowsAsync`-tested). `_numCollections` (plain int, mutated only
  under the gather lock) is the first-gather flag.
- `IConsoleInteraction` isolates stdin prompts (`ConsoleInteraction` impl,
  `FakeConsoleInteraction` in tests); `PROMCLIENT_NONINTERACTIVE=1` or a
  non-TTY makes missing credentials throw instead of prompt.
- Config key changes ripple: JSON keys are Java-parity (`name`, `interval`,
  `prefix`, `multi_row`, `enabled`); provider keys are new
  (`provider`, `connection_string`, `odbc_driver`, `driver_assembly`,
  `driver_factory`; legacy `driver_class`/`driver_uri` still parsed, label-only).
  Port `9853` is hardcoded in ~8 places (Program, csproj default, Dockerfile,
  k8s manifests, chart values/templates, e2e configs).
- `src/PromExporter.Jdbc/appsettings.json` is deceptive: the exporter never
  reads IConfiguration. Only `config.json` + env vars matter.

## Important Files

- `src/PromExporter.Jdbc/Program.cs` — endpoints, banner, SIGTERM shutdown, port-busy exit 1.
- `src/PromExporter.Jdbc/ExporterConfig.cs` — every config-key/env precedence
  (port: `PORT` → `PROMCLIENT_PORT` → JSON `port` → 9853).
- `PLAN-dotnet-rewrite.md` — behavioral surface + documented deviations; read §1/§10 before changing gather/naming semantics.
- `README.md` — user-facing config table under `## JSON Configuration`; update
  it together with config-key changes.
- `charts/prometheus-exporter-jdbc/Chart.yaml` — `version:` drives the OCI
  publish workflow; bump on chart changes.
- `Dockerfile` — non-root uid 10001 (must match chart `runAsUser`),
  `ENV ASPNETCORE_URLS=` blanked so `UseUrls` in code wins,
  `ENTRYPOINT dotnet PromExporter.Jdbc.dll` (no apphost).

## Runtime/Tooling Preferences

- .NET 10 SDK required (CI pins `10.0.x`); Docker for the e2e/parity gates.
- No package manager beyond NuGet; `Directory.Packages.props` absent — versions
  live in the two csproj files.
- The parity harness needs a locally-built base image `prom-java-local`
  (pre-rewrite Java jar at `/app/app.jar`) — built nowhere in this repo; without
  it both compose builds fail. Driver jars and `tests/e2e/db/` are gitignored;
  the scripts fetch/create them (`java-sqlite-image/fetch-drivers.sh`).

## Testing & QA

- xUnit 2.9.3 (`<Using Include="Xunit"/>` — no `using Xunit;` in files).
  Two tiers: scripted `FakeDb` unit tests (`FakeDb.Script` keyed by exact
  command text, `Executed` log for ordering asserts) and
  `SqliteIntegrationTests` (real Microsoft.Data.Sqlite, production-shaped
  wiring incl. a parallel `/metrics_now` regression test; cleanup via
  `SqliteConnection.ClearAllPools()`).
- Env-var isolation is by convention: **every env-touching test lives in
  `ExporterConfigTests`** and uses `EnvScope` — xUnit parallelizes across
  classes, serializes within one. Add new env-dependent tests there, or keep
  them env-free.
- Timing tests poll (`WaitUntilAsync`, 10 s deadlines), never fixed-sleep
  assert.
- The e2e gate (`run-e2e.sh`) asserts via Prometheus API (`STAT__CNT == 42`,
  multi-row `POOL__QWORK__SIZE`, a `job="dotnet-metrics-now"` series proving
  `/metrics_now` works) and always tears down. The parity gate
  (`parity-check.sh`) diffs normalized `/metrics` between the Java reference
  (host port 19853) and .NET (19854) across update/error/recovery rounds; its
  6 s sleeps are tuned to the 5 s `/metrics_now` tolerance — don't shorten
  them. Both configs pin `hostname` in JSON because Docker injects `HOSTNAME`.
- Neither gate runs in CI yet; run both locally before touching gather,
  naming, or exposition code.
