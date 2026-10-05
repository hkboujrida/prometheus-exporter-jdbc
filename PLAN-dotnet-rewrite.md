# .NET (C#) Rewrite Plan — prometheus-exporter-jdbc

Rewrite the Java/JDBC Prometheus exporter as a .NET 10 minimal-API app using
`System.Data.Common` providers, with Docker support. Wire-compatible with the
existing config.json, metric names, endpoints, k8s manifests, and Helm chart.

## 1. Behavioral surface to port (parity spec, from current source)

### HTTP
- `GET /` and `GET /metrics` → Prometheus text format from the default registry.
- `GET /metrics_now` → run gather for all populators **in parallel**, join, then serve
  (`SQLMetricPopulator.gatherNow(5000)`: skip gather if last success < 5 s ago).
- Disable HTTP TRACE.
- On port bind failure: error + exit 1. On startup success print the
  `=== Successfully started Prometheus client on port N ===` banner.

### Config (`Config.java`)
- File: `config.json` in CWD by default; overridable (`promclient.config` → env
  `PROMCLIENT_CONFIG` / `--config`). If missing: offer to write bundled defaults.
- Port precedence: `PORT` env → `promclient.port` sysprop (→ `PROMCLIENT_PORT` env)
  → `port` in JSON → 9853.
- `queries[]` keys: `sql` (required), `name`, `interval` (seconds; default
  effectively infinite → on-demand only), `prefix`, `include_hostname` (default
  **false**), `enabled` (default true), `multi_row` (default false).
- Credentials resolution order (per key): JSON value → platform default (IBM i:
  `localhost`/`*CURRENT`) → env (`HOSTNAME`, `USERNAME`, `PASSWORD`) → interactive
  prompt. JSON password → warn "NOT SECURE".
- `driver_class` default `com.ibm.as400.access.AS400JDBCDriver`, `driver_uri`
  default `jdbc:as400://localhost` (see §3 for the .NET translation).

### Collector (`SQLMetricPopulator.java`)
- Split `sql` on literal `"; "`: all parts but the last are **setup statements**
  executed every gather cycle (e.g. `CALL QSYS2.DUMP_PLAN_CACHE_PROPERTIES(...)`);
  the last part is the prepared main query.
- **Single-row mode**: at registration, read result-set metadata; register one
  gauge per numeric column (help = column label). Only the first row is read per
  gather.
- **Multi-row mode**: gauges created dynamically per gather; the first column
  value becomes a `rowname` component of the gauge name.
- Numeric columns: JDBC `Types.{BIGINT,INTEGER,DOUBLE,FLOAT,NUMERIC,REAL,SMALLINT,TINYINT,DECIMAL}`
  → CLR: `sbyte/byte/short/ushort/int/uint/long/ulong/float/double/decimal`.
- Gauge naming: `[hostname__][prefix__][rowname__]column`, then strip everything
  not `[A-Za-z0-9_]`. Hostname component truncated at first `.`.
- Every gauge carries labels `hostname` (dot-truncated) and `driver_class`.
- Error during gather → **unregister all that query's gauges** (metrics disappear
  while DB is down) and log.
- Interval thread starts only after the **first successful** gather; loop =
  sleep(interval) → gather.
- Startup: verify every populator once (`gatherNow(12)`) before serving.

### Misc
- `sc` subcommand: dump a Service Commander `prometheus.yml` definition +
  registration instructions (IBM i only; keep or drop, see §6).

## 2. Target stack

| Concern | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 (LTS, current as of Nov 2025) | supported through Nov 2027+ incl. Linux extended servicing |
| HTTP | ASP.NET Core minimal API + Kestrel | replaces Jetty; free health/metrics endpoint control |
| Metrics | `prometheus-net` | registry/gauge/text format parity |
| DB access | `System.Data.Common` (`DbProviderFactory`) | the .NET analogue of JDBC Driver/DriverManager |
| Config | `System.Text.Json`, case-insensitive | same config.json |
| Logging | `Microsoft.Extensions.Logging` console | replaces AppLogger |
| Tests | xUnit | golden-file + SQLite integration |

Layout:

```
src/PromExporter.Jdbc/
  Program.cs            # bootstrap, banner, Kestrel, endpoints, SIGTERM drain
  ExporterConfig.cs     # config.json model + env/CLI/defaults resolution
  DbConnectionProvider.cs # assembly-loading factory, reconnect-on-failure
  QueryCollector.cs     # SQL split, single/multi-row, interval loop, gatherNow
  GaugeNaming.cs        # sanitize + prometheus-net name validation
  default-config.json   # embedded IBM i defaults (same queries)
tests/PromExporter.Jdbc.Tests/
Dockerfile  .dockerignore  PromExporter.sln
```

## 3. JDBC → ADO.NET provider mapping

Java's `driver_class`/`driver_uri` has no direct .NET equivalent (no global
classpath). New config keys, same resolution philosophy:

- `driver_assembly`: path to a managed provider DLL, loaded via
  `Assembly.LoadFrom`; `driver_factory`: `Type.FullName` of the
  `DbProviderFactory` (uses its public static `Instance` field/property).
- `driver_uri` / `username` / `password` → connection string built as
  `driver_uri` + credentials merged per-provider (`User ID=`, `Password=` appended
  when the URI doesn't already contain them).
- Fidelity quirk to preserve: when no `driver_uri` is set, the Java code passes
  `hostname` as the JDBC URL — do the same (use `hostname` as connection string).

IBM i connectivity (the default use case):

- **Default: IBM DB2 .NET provider** — NuGet `IBM.Client` (`IBM.Data.DB2`),
  supports IBM i, needs the ACS CLI native libraries in the image.
- **Fallback: ODBC** — `IBM.Data.Odbc` (in-box package) + IBM i Access ODBC
  driver installed in the container image (ACS Linux ODBC tarball in the Docker
  build). Connection string: `DRIVER={IBM i Access ODBC Driver};SYSTEM=...;UID=...;PWD=...`.
- Dev/test provider: `Microsoft.Data.Sqlite` bundled for integration tests.
- Reading a plain-JDBC `config.json` with `driver_class: com.ibm.as400...`:
  warn once "legacy JDBC keys ignored; using <built-in default provider>".

## 4. Porting details / gotchas

- **Gauge name validation**: prometheus-net rejects names not matching
  `[a-zA-Z_:][a-zA-Z0-9_:]*`. Java's sanitizer can yield names starting with a
  digit. Fix in sanitizer: after stripping, prefix `_` if leading char is a digit.
- **Help text**: Java uses column *label* as help; prometheus-net bakes help at
  creation — same behavior, fine.
- **Dynamic gauges in multi-row mode**: prometheus-net creates a child per label
  set; here names are distinct per row, so we create new `Gauge` instances and
  `MetricFactory`-managed registration; on gather error, remove via
  `Prometheus.Metrics` registry removal (track disposable `MetricHandle`s from
  `Metrics.CustomGrouping`/`Untyped` or keep a per-collector
  `CollectorRegistry`-scoped factory — decide during M2 spike; prometheus-net's
  dynamic-metric support is the one API risk in the rewrite).
- **Single-row registration bug for free**: Java logs "no numeric data" only if
  the *first* column is non-numeric (in-loop size check). Rewrite: warn after
  scanning all columns.
- **Concurrency**: per-collector `SemaphoreSlim(1,1)` replacing `m_requestLock`;
  interval loop as `Task` + `PeriodicTimer`; `/metrics_now` =
  `Task.WhenAll(collectors.Select(c => c.GatherNowAsync(5000)))`.
- **Numeric conversion**: `reader.GetDouble(i)` semantics — Java returns 0 for
  NULL; use `IsDBNull` check → 0.
- **Non-interactive in containers**: skip prompts when
  `Console.IsInputRedirected` or `PROMCLIENT_NONINTERACTIVE=1`; fail fast with a
  clear message instead of blocking stdin.
- `include_hostname`, label values, `; ` split, 5 s `/metrics_now` tolerance,
  first-gather-before-serve, error→unregister: copy exactly.

## 5. Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PromExporter.sln ./
COPY src/PromExporter.Jdbc/ src/PromExporter.Jdbc/
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
COPY src/PromExporter.Jdbc/default-config.json ./config.json.default
RUN useradd -r -u 10001 exporter && chown -R exporter /app
USER exporter
EXPOSE 9853
ENV PORT=9853
ENTRYPOINT ["./PromExporter.Jdbc"]
```

- Image options: default `aspnet:10.0` (Ubuntu, has curl for HEALTHCHECK);
  chiseled variant later (no HEALTHCHECK shell, rely on k8s probes).
- Optional build arg `IBM_I_ACCESS=1`: extract ACS ODBC/CLI driver into the image
  for out-of-box IBM i support (mirrors the bundled JT400 in the Java jar).
- `HEALTHCHECK CMD curl -fs http://127.0.0.1:${PORT}/metrics || exit 1` (default
  variant only).
- Same env contract as today: `PORT`, `USERNAME`, `PASSWORD`, `HOSTNAME`,
  mount `config.json` at `/app/config.json` → **existing `docker run`, k8s
  manifests, probes (`/metrics`), and Helm chart work unchanged**; only the
  image tag build source changes.

## 6. Repo/CI impact

- Delete: `pom.xml`, `src/main/java/**`, Java-specific Dockerfile layers.
- `.github/workflows/maven-ci.yml` → `dotnet-ci.yml` (`actions/setup-dotnet@v4`,
  `dotnet test`). `docker-publish.yml`: add setup-dotnet before
  `docker/build-push-action` (same image name `ghcr.io/hkboujrida/prometheus-exporter-jdbc`).
  `build-release.yml` publishes `dotnet publish` zip per RID instead of fat jar.
- `azure-pipelines*.yml`: swap Maven `Maven@3` task for `DotNetCoreCLI@2` (build+test) — mechanical.
- `k8s/`, `charts/`: no changes (same port, path, image).
- Service Commander (`sc`): drop from v1 unless .NET-on-IBM-i (PASE, ACS RPM) is a
  target; the Service Commander YAML can still ship, pointing at the published
  single-file binary. Decision point, default = keep subcommand writing the same
  file with a `dotnet` command line.
- README: rewrite install/usage sections for `dotnet`/Docker; keep the metric
  lists and config table (identical keys minus `driver_class`/`driver_uri`
  translation, documented as the one breaking change).

## 7. Milestones

1. **M1 — Skeleton + config**: solution, `ExporterConfig` with full
   resolution-order parity, `GaugeNaming` + golden unit tests (name sanitization,
   dot-truncation, leading-digit fix, default-config byte-compat with today's
   `config.json`).
2. **M2 — Collector engine**: provider loading, SQL split, single/multi-row
   gather, interval loop, error→unregister, `gatherNow` tolerance. Integration
   tests against SQLite (multi-statement via setup-statements, NULL→0,
   non-numeric skip).
3. **M3 — HTTP + parity**: endpoints incl. `/metrics_now`, TRACE off, banner,
   exit codes, `sc`. **Parity check: scrape Java exporter and .NET exporter
   against the same DB, `diff` `/metrics` after name normalization — must be
   empty.**
4. **M4 — Docker + CI/CD + docs**: Dockerfile variants, workflow swaps,
   azure-pipelines swap, README. Deploy to a k8s cluster and confirm chart
   installs with just an image-tag change.

Effort: ~720 LOC Java → ~800–900 LOC C# + tests; 3–5 focused days.

## 8. Risks

- **prometheus-net dynamic metric creation/removal** (multi-row mode,
  error-unregister) is the only non-straightforward API mapping — spike in M2
  day 1; fallback = implement a tiny custom `Collector` for dynamic gauges.
- **IBM i .NET/ODBC native drivers** are non-managed, x86-64/arm64 Linux only —
  no pure-managed IBM i option; acceptable since containers are the deployment
  target, but IBM-i-PASE-native runs need the RPM route (or keep the Java jar for
  that niche).
- IBM i default credentials (`*CURRENT`, localhost) can't be replicated →
  env/prompt required off-PASE; document as a behavior change.

## 9. Testing strategy

No containerized IBM i exists (Wazi aaS is z/OS; IBM Cloud sandbox has no IBM i),
so automated tests use Db2 CE + SQLite as stands, and IBM i is a manual smoke
step on real hardware.

### L1 — Unit (xUnit, no I/O, every commit)
- `GaugeNaming`: golden tests — strip non-`[A-Za-z0-9_]`, `__` joins, hostname
  dot-truncation, leading-digit `_` prefix (prometheus-net would throw otherwise).
- `ExporterConfig`: port precedence (env/sysprop/JSON/default), credential
  chains incl. PASSWORD/USERNAME/HOSTNAME env, `enabled=false` skip, interval
  default, legacy `driver_class` warning path.
- SQL split on `"; "`: 0/1/N statements, trailing-statement selection.
- Numeric CLR type → collect/skip table.
- Stub `DbProviderFactory`/`DbConnection`/`DbDataReader` for collector logic
  without any real DB: setup-statement order, single-row reads only row 1,
  multi-row first-column→gauge-name, NULL→0, error→unregister-all,
  `gatherNow(5000)` tolerance skip, interval loop (injectable clock/delay).
- Metrics assertions in-process: own `CollectorRegistry` per test (no global
  pollution), scrape via `CollectAndAppendAsync` → assert text.

### L2 — Integration vs real DBs (xUnit + Testcontainers)
- **SQLite** (`Testcontainers` not even needed, file-backed): fast lane, every
  commit. Covers: end-to-end query→gauge, multi-statement setup via temp tables,
  numeric/NULL/DECIMAL handling, multi-row, DB-down → gauges vanish → reconnect.
- **Db2 CE** (`Testcontainers.Db2`, image `icr.io/db2_community/db2` —
  `ibmcom/db2` is sunsetting): validates the real `IBM.Client` provider,
  IBM-family types/dialect. Image is x86-64-only, ~2.5 GB, slow start →
  `[Trait("Category","db2")]`, nightly job, not the PR gate.

### L3 — E2E against the published image + real Prometheus
Compose harness (`tests/e2e/docker-compose.test.yml`): Db2 CE seed DB +
exporter image (mounted config.json) + stock `prometheus` scraping `/metrics`
every 5 s + a second job scraping `/metrics_now`. Assert via Prometheus HTTP
API (`/api/v1/targets`, `/api/v1/query`):
- both targets UP;
- expected series exist with `hostname`/`driver_class` labels matching regexes
  generated from config;
- interval semantics: value does NOT change between scrapes faster than
  `interval` (point-in-time cache);
- point-in-time: mutate DB, scrape `/metrics_now`, value changed despite huge
  `interval`; second scrape within 5 s unchanged (tolerance);
- TRACE → 405/401 (`curl -X TRACE`); non-root user; SIGTERM exits 0.

### L4 — Java↔.NET parity gate (release blocker)
Compose runs **both** exporters (old jar + new image) against the same Db2
seed DB with identical config; `diff <(normalize(java /metrics)) <(normalize(dotnet /metrics))`
must be empty — normalization only sorts lines and drops no values. This is the
acceptance proof the rewrite is faithful, including multi-row gauges and
error-gauge-removal (stop the DB, confirm both lose the same series).

### L5 — IBM i smoke (manual, your box)
`docker run` the image against a real IBM i with
`IBM_I_ACCESS=1` build (ACS ODBC), default IBM i config.json; expect the
README's metric list (TOTAL_JOBS_IN_SYSTEM, …) present via
`curl :9853/metrics | promtool`. Document the checklist in TESTING.md; not CI.

### CI wiring
- PR job: L1 + L2-SQLite + L3 (image built once, Db2 compose) — <10 min.
- Nightly: + L2-Db2, + L4 parity.
- Release: L4 must pass before `docker-publish` tags a version.

## 10. Implementation status (post-rewrite)

- L1 unit + SQLite integration: 52 tests, `dotnet test`.
- E2E with real Prometheus: `tests/e2e/docker-compose.test.yml`.
- Java parity gate L4: `tests/e2e/parity-check.sh` — green on all five scenarios.
- Verified deviations from the Java exporter (documented in README):
  1. Single-row gauges RECOVER after a DB failure (Java: permanent unregister).
  2. `driver_class` label value reflects the configured provider/factory
     (legacy configs keep the old JDBC class name automatically).
  3. Non-interactive environments fail fast instead of prompting.
- Deviations discovered by the gate and fixed during implementation:
  - One connection per collector (matches Java per-populator ConnectionManager);
    a shared connection raced under `/metrics_now` Task.WhenAll.
  - prometheus-net name validation forced the leading-digit `_` prefix fix.
