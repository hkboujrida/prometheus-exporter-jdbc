# JDBC Prometheus Exporter

[![Docker Build & Push](https://github.com/hkboujrida/prometheus-exporter-jdbc/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/hkboujrida/prometheus-exporter-jdbc/actions/workflows/docker-publish.yml)

Prometheus exporter for IBM i and other databases. This provides an interface for passive metrics collection. That is, Prometheus can scrape this exporter for metrics.

Any database reachable through an ADO.NET provider may be used as a data
source. The metrics are customizable.
Any numeric metric available through SQL can be monitored using this client!

This exporter was built for and tested on IBM i as a way to monitor IBM i
system and application status through SQL (the original implementation was
Java/JDBC; this .NET rewrite keeps the same config format, metric names,
endpoints, and Docker contract).

- [Installation and Startup](#installation-and-startup)
  - [Known Breaking Changes](#known-breaking-changes)
  - [Using a database provider](#using-a-database-provider)
- [Running on a different port](#running-on-a-different-port)
- [Prometheus Configuration](#prometheus-configuration)
- [JSON Configuration](#json-configuration)
  - [Valid values for JSON configuration](#valid-values-for-json-configuration)
    - [`queries` element](#queries-element)
- [Collection data modes](#collection-data-modes)
- [Collection timing modes](#collection-timing-modes)
- [Managing with Service Commander (IBM i only)](#managing-with-service-commander-ibm-i-only)

## Installation and Startup

Option A — Docker (recommended): see [Docker Deployment](#docker-deployment).

Option B — standalone binary. Download the latest
`prometheus-exporter-jdbc-linux-x64.zip` from
[the releases page](https://github.com/hkboujrida/prometheus-exporter-jdbc/releases),
unzip, and run:
```bash
./PromExporter.Jdbc
```
Create a default configuration file by responding `y` to the
following prompt:
```bash
Configuration file config.json not found. Would you like to initialize one with defaults? [y]
```
You should see a series of messages about collectors being registered. If you see the
following message, the client is running successfully:
```
==============================================================
Successfully started Prometheus client on port 9853
==============================================================
```

If you're not running on IBM i, you'll want to kill the program and modify
`config.json` to have reasonable values for your needs.

In non-interactive environments (containers, systemd), set
`PROMCLIENT_NONINTERACTIVE=1`; missing required values then fail fast with a
clear error instead of prompting.

## Docker Deployment

A Docker image is automatically built and published to GitHub Container Registry (GHCR) on every push to the main branch.

### Running with Docker

```bash
docker run -p 9853:9853 \
  -e USERNAME=your_username \
  -e PASSWORD=your_password \
  -e HOSTNAME=your_system_name \
  -v /path/to/config.json:/app/config.json:ro \
  ghcr.io/hkboujrida/prometheus-exporter-jdbc:latest
```

Recognized environment variables: `PORT`, `HOSTNAME`, `USERNAME`, `PASSWORD`,
`PROMCLIENT_PORT`, `PROMCLIENT_CONFIG` (path), `PROMCLIENT_NONINTERACTIVE`,
`PROMCLIENT_VERBOSE`.

### CI/CD Options

- **GitHub Actions**: Automatically builds and pushes to GHCR (see `.github/workflows/docker-publish.yml`)
- **Azure DevOps**: Pipeline for Harbor registry (see `azure-pipelines.yml` and `azure-devops-setup.md`)

### Kubernetes Deployment

Kubernetes manifests are provided in the `k8s/` directory. See `k8s/README.md` for detailed deployment instructions.

### Helm Chart Deployment

A Helm chart is available for standard Kubernetes deployment.

```bash
# Login to GHCR
helm registry login ghcr.io -u <username> -p <token>

# Install the chart
helm install my-exporter oci://ghcr.io/hkboujrida/charts/prometheus-exporter-jdbc --version <version>
```

See [charts/prometheus-exporter-jdbc/README.md](charts/prometheus-exporter-jdbc/README.md) for configuration details.

### Known breaking changes

**In the .NET rewrite (current)**
- Java/JDBC configuration keys are replaced by provider configuration (see
  [Using a database provider](#using-a-database-provider)). Legacy
  `driver_class`/`driver_uri` in an existing `config.json` are accepted:
  `driver_class` only supplies the `driver_class` metric label, `driver_uri`
  is used as the connection string.
- After a database failure, gauges re-register automatically on the next
  successful gather. (The old Java client permanently unregistered single-row
  gauges after a gather error.)
- The `promclient.port` Java system property is replaced by the
  `PROMCLIENT_PORT` environment variable.

**In version 1.0**
- The default value for `include_hostname` is now `false`. By default, the host name will not be included in the metric names (as the host name is included in the label). This may break existing configurations.
- The SQL query specified can now contain multiple SQL statements, separated by `; `. This is implemented by a dump "split" and not by any SQL parser logic. As such, this will break any existing SQL query that correctly contains this string.

### Using a database provider

The exporter ships with two providers:

- **`odbc`** (default) — for IBM i via the
  [IBM i Access Client Solutions ODBC driver](https://www.ibm.com/support/pages/ibm-i-access-client-solutions)
  (unixODBC-based, Linux x86-64/arm64). With a bare `hostname`/`username`/`password`
  the exporter builds
  `DRIVER={IBM i Access ODBC Driver};SYSTEM=<hostname>;UID=...;PWD=...`;
  override the driver name via `odbc_driver` or give a full `connection_string`.
- **`sqlite`** — handy for demos/tests; `connection_string` (or `hostname`) is the file path.

Any other ADO.NET provider (e.g. IBM's `IBM.Data.DB2` .NET provider for IBM i,
Oracle, Postgres) is loaded dynamically:

```json
{
  "provider": "custom",
  "driver_assembly": "/drivers/IBM.Client.dll",
  "driver_factory": "IBM.Data.DB2.Core.DB2CoreClientLibraryFactory",
  "connection_string": "Server=myas400:8471;Database=MYLIB;UID=user;PWD=secret;"
}
```

For IBM i inside Docker, build the image with the ACS ODBC driver installed,
or run the exporter on a host that has it and point `hostname` at your system.

### Running headless
If you would like to run the program in the background so that you can exit
your shell and keep the Prometheus client running, you can use the `nohup` utility:
```bash
nohup ./PromExporter.Jdbc > prom-client.log 2>&1
```

## Running on a different port

The Prometheus client port can be customized in several ways. The port
is determined by the following, in order of precedence:
- The `PORT` environment variable
- The `PROMCLIENT_PORT` environment variable
- The `port` value of the JSON configuration file
- The default value of 9853

## Prometheus Configuration

To configure Prometheus, just add a target to the `scrape_configs` as done
in the following sample configuration:

```yaml
scrape_configs:
  - job_name: 'prometheus-jdbc'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['1.2.3.4:9853']
```

## JSON Configuration

See [default-config.json](src/PromExporter.Jdbc/Resources/default-config.json) for the example JSON file written at first startup
(IBM i default queries against `QSYS2` service functions — see the
[metrics list](#metrics-gathered-with-default-config-ibm-i)).

### Valid values for JSON configuration

| Key name           | Type     | required? | Description                                      |
| ------------------ | -------- | ----------| -------------------------------------------------|
| `queries`          | array    | yes       | Array of elements specifying which SQL queries to run |
| `port`             | Integer  | no        | TCP port to serve on (default 9853)              |
| `provider`         | String   | no        | `odbc` (default), `sqlite`, or `custom` (`db2` aliases `custom`) |
| `connection_string`| String   | no        | Full provider connection string; overrides built defaults |
| `odbc_driver`      | String   | no        | ODBC driver name (default `IBM i Access ODBC Driver`) |
| `driver_assembly`  | String   | no        | Path to a custom ADO.NET provider assembly       |
| `driver_factory`   | String   | no        | `DbProviderFactory` type inside `driver_assembly` |
| `hostname`         | String   | no        | System to connect to / display in metric labels (default: `HOSTNAME` env or prompt) |
| `username`         | String   | no        | Username for the connection (`USERNAME` env or prompt) |
| `password`         | String   | no        | Password for the connection. **NOT SECURE** (`PASSWORD` env or prompt) |

**NOTE: The program fails fast if a needed value (for instance a password) is
neither configured, in the environment, nor promptable on an interactive TTY.**

#### `queries` element
The `queries` element contains an array. Each element in the array can have the following values:
| Key name           | Type     | required? | Description                                      |
| ------------------ | -------- | ----------| -------------------------------------------------|
| `sql`              | String   | yes       | The SQL query                                    |
| `name`             | String   | no        | A human-readable name for the query              |
| `interval`         | Integer  | no        | The interval to wait between queries (default: infinity) |
| `prefix`           | String   | no        | A prefix to be used in the Prometheus gauge name |
| `include_hostname` | boolean  | no        | Whether to include the hostname in the Prometheus gauge name (default: false) |
| `enabled`          | boolean  | no        | Whether this SQL query is enabled (default: true) |
| `multi_row`        | boolean  | no        | Whether to enable multi-row mode (default: false) |

**IMPORTANT NOTES ABOUT COLLECTED METRICS**
- Only numeric values will be collected
- The values will be reported to Prometheus in the format
```
hostname__prefix_identifier
```
(where `hostname` is the configured/system host name and `column` is the SQL column)
- You can tailor the metric name in prometheus by changing the column name via the SQL query (using the SELECT `AS XXXX` syntax)
- The `prefix` is only included if specified in the configuration
- The `hostname` can be included via configuration
- The `identifier` is the column name when running in single-row mode

## Collection data modes

### Single-row mode

The default behavior for processing query output is single-row mode. If feasible, this is the recommended
way to collect metrics.
In single-row mode, only the first row of results are processed, but the gauge names can be computed up front,
since the identifier for the gauge is simply the column name.

### Multi-row mode

Multi-row mode allows you to collect metrics from multiple rows of a query. When using multi-row mode,
the value of the first column in each result is used to formulate a gauge name each time the query is run.
This has negative implications if the result set data does not have a consistent value in the first column.

## Collection timing modes

### Interval-based

The `interval` value for a query in the JSON configuration indicates how long to pause between queries.
When using this technique, responses to Prometheus will be based on the most recent value collected.
This could mean the same collection value is repeated in Prometheus, or that values may be lost. For
instance, if using a 60-second query interval:
- If Prometheus scrapes every two minutes, some values will not be propagated to Prometheus
- If Prometheus scrapes every 30 seconds, the same collected value may be reported more than once

For efficiency's sake, this is the default behavior. This allows for aggressive Prometheus scrape intervals
without putting excessive load on the monitored system.

### Point-in-time

If omitted, the `interval` value for a query in the JSON configuration defaults to infinity. Metrics can be
gathered upon-request by way of the `/metrics_now` endpoint. This approach provides the most up-to-date
information to Prometheus, at intervals defined by Prometheus. However, use this approach with caution.
It exposes the monitored system to excessive load if metrics are scraped often. When using this approach,
the exporter still limits the query frequency to 5-second intervals.

## Managing with Service Commander (IBM i only)

Run the exporter binary with the `sc` argument:
```bash
./PromExporter.Jdbc sc
```
Follow the on-screen instructions. A `prometheus.yml` file will be created.
Do not delete this file.

The default configuration adds a `prometheus` to the `autostart` group
to be launched automatically at IPL.

# Metrics gathered with default config (IBM i)

- TOTAL_JOBS_IN_SYSTEM
- MAXIMUM_JOBS_IN_SYSTEM
- ACTIVE_JOBS_IN_SYSTEM
- INTERACTIVE_JOBS_IN_SYSTEM
- ELAPSED_TIME
- ELAPSED_CPU_USED
- ELAPSED_CPU_SHARED
- ELAPSED_CPU_UNCAPPED_CAPACITY
- CONFIGURED_CPUS
- CURRENT_CPU_CAPACITY
- AVERAGE_CPU_RATE
- AVERAGE_CPU_UTILIZATION
- AVERAGE_CPU_RATE_REAL
- AVERAGE_CPU_UTILIZATION_REAL
- MINIMUM_CPU_UTILIZATION
- MAXIMUM_CPU_UTILIZATION
- SQL_CPU_UTILIZATION
- MAIN_STORAGE_SIZE
- SYSTEM_ASP_STORAGE
- TOTAL_AUXILIARY_STORAGE
- SYSTEM_ASP_USED
- CURRENT_TEMPORARY_STORAGE
- MAXIMUM_TEMPORARY_STORAGE_USED
- PERMANENT_ADDRESS_RATE
- TEMPORARY_ADDRESS_RATE
- TEMPORARY_256MB_SEGMENTS
- TEMPORARY_4GB_SEGMENTS
- PERMANENT_256MB_SEGMENTS
- PERMANENT_4GB_SEGMENTS
- TEMPORARY_JOB_STRUCTURES_AVAILABLE
- PERMANENT_JOB_STRUCTURES_AVAILABLE
- TOTAL_JOB_TABLE_ENTRIES
- AVAILABLE_JOB_TABLE_ENTRIES
- IN_USE_JOB_TABLE_ENTRIES
- ACTIVE_JOB_TABLE_ENTRIES
- JOBQ_JOB_TABLE_ENTRIES
- OUTQ_JOB_TABLE_ENTRIES
- JOBLOG_PENDING_JOB_TABLE_ENTRIES
- PARTITION_ID
- NUMBER_OF_PARTITIONS
- ACTIVE_THREADS_IN_SYSTEM
- PARTITION_GROUP_ID
- SHARED_PROCESSOR_POOL_ID
- DEFINED_MEMORY
- MINIMUM_MEMORY
- MAXIMUM_MEMORY
- MEMORY_INCREMENT
- PHYSICAL_PROCESSORS
- PHYSICAL_PROCESSORS_SHARED_POOL
- MAXIMUM_PHYSICAL_PROCESSORS
- DEFINED_VIRTUAL_PROCESSORS
- VIRTUAL_PROCESSORS
- MINIMUM_VIRTUAL_PROCESSORS
- MAXIMUM_VIRTUAL_PROCESSORS
- DEFINED_PROCESSING_CAPACITY
- PROCESSING_CAPACITY
- UNALLOCATED_PROCESSING_CAPACITY
- MINIMUM_REQUIRED_PROCESSING_CAPACITY
- MAXIMUM_LICENSED_PROCESSING_CAPACITY
- MINIMUM_PROCESSING_CAPACITY
- MAXIMUM_PROCESSING_CAPACITY
- PROCESSING_CAPACITY_INCREMENT
- DEFINED_INTERACTIVE_CAPACITY
- INTERACTIVE_CAPACITY
- INTERACTIVE_THRESHOLD
- UNALLOCATED_INTERACTIVE_CAPACITY
- MINIMUM_INTERACTIVE_CAPACITY
- MAXIMUM_INTERACTIVE_CAPACITY
- DEFINED_VARIABLE_CAPACITY_WEIGHT
- VARIABLE_CAPACITY_WEIGHT
- UNALLOCATED_VARIABLE_CAPACITY_WEIGHT
- THREADS_PER_PROCESSOR
- DISPATCH_LATENCY
- DISPATCH_WHEEL_ROTATION_TIME
- TOTAL_CPU_TIME
- INTERACTIVE_CPU_TIME
- INTERACTIVE_CPU_TIME_ABOVE_THRESHOLD
- UNUSED_CPU_TIME_SHARED_POOL
- JOURNAL_RECOVERY_COUNT
- JOURNAL_CACHE_WAIT_TIME
- REMOTE_CONNECTIONS

# Testing

- Unit + SQLite integration tests: `dotnet test`
- Prometheus end-to-end: `docker compose -f tests/e2e/docker-compose.test.yml up --build --abort-on-container-exit --exit-code-from assert`
- Java↔.NET output parity gate: `tests/e2e/parity-check.sh`

# Sample screenshot (visualization w/Grafana)

![image](https://user-images.githubusercontent.com/17914061/180038306-30724eae-83b2-42c3-b6d5-da2e9b239a25.png)
