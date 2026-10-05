using System.Data.Common;
using System.Globalization;
using Prometheus;

namespace PromExporter.Jdbc;

/// <summary>
/// Executes one configured SQL query on an interval (and on demand) and pushes
/// numeric columns to Prometheus gauges. Mirrors SQLMetricPopulator.java:
///  - sql split on literal "; " -> all but last are per-cycle setup statements
///  - single-row mode: first row only, gauge per numeric column
///  - multi-row mode: one gauge per (row, numeric column), first column names the row
///  - gather error: all of this query's gauges vanish until the next successful
///    gather (Java parity: unregister on failure)
///  - interval loop starts only after the first successful gather
///  - GatherNow skips work when the last success is younger than the tolerance
/// prometheus-net has no public metric-removal API, so each collector owns its
/// own CollectorRegistry; on error a fresh registry replaces the old one.
/// /metrics concatenates the per-collector expositions (disjoint families).
/// </summary>
public sealed class QueryCollector : IAsyncDisposable
{
    public static readonly string[] LabelNames = ["hostname", "driver_class"];

    private readonly IDbConnectionProvider _conn;
    private readonly ExporterConfig _cfg;
    private readonly SqlQueryConfig _q;
    private readonly ILogger _log;
    private readonly Dictionary<string, Gauge> _gauges = [];
    private readonly SemaphoreSlim _gatherLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private CollectorRegistry _registry;
    private IMetricFactory _factory;
    private long _lastSuccessTimestamp; // Environment.TickCount64, 0 = never
    private int _numCollections;
    private Task? _intervalLoop;

    public QueryCollector(IDbConnectionProvider conn, ExporterConfig cfg, SqlQueryConfig q, ILogger log)
    {
        _conn = conn;
        _cfg = cfg;
        _q = q;
        _log = log;
        (SetupStatements, MainSql) = SplitSql(q.Sql);
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);
    }

    public IReadOnlyList<string> SetupStatements { get; }
    public string MainSql { get; }

    /// <summary>Export this collector's current exposition text (empty after an error).</summary>
    public Task ExportAsync(Stream stream, CancellationToken ct = default) => _registry.CollectAndExportAsTextAsync(stream, ct);

    /// <summary>Java parity: dump-split on "; "; the last part is the metrics query.</summary>
    public static (List<string> Setup, string Main) SplitSql(string sql)
    {
        var parts = sql.Split("; ").ToList();
        var main = parts[^1];
        parts.RemoveAt(parts.Count - 1);
        return (parts, main);
    }

    /// <summary>
    /// Gather unless the last success is younger than toleranceMs.
    /// A connection failure on the very first gather throws (startup
    /// verification must exit non-zero, like the Java exporter); later
    /// failures are logged and the gauges stay as last reported.
    /// </summary>
    public async Task GatherNowAsync(int toleranceMs, CancellationToken ct = default)
    {
        var last = Interlocked.Read(ref _lastSuccessTimestamp);
        if (last != 0 && Environment.TickCount64 - last <= toleranceMs)
            return;
        await GatherAsync(throwOnConnectError: _numCollections == 0, ct).ConfigureAwait(false);
    }

    private async Task GatherAsync(bool throwOnConnectError, CancellationToken ct)
    {
        DbConnection conn;
        await _gatherLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            conn = await _conn.GetOpenConnectionAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            if (throwOnConnectError) throw;
            _log.LogError("ERROR!! ABORTING COLLECTION!! Cause: database unreachable");
            return;
        }

        try
        {
            _log.LogDebug("gathering metrics... {Sql}", MainSql);
            foreach (var stmt in SetupStatements)
            {
                await using var setupCmd = conn.CreateCommand();
                setupCmd.CommandText = stmt;
                await setupCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = MainSql;
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var hostLabel = GaugeNaming.HostLabel(_cfg.DisplayHostname);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                string rowName = "";
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    string columnName;
                    string gaugeName;
                    if (_q.MultiRow)
                    {
                        if (i == 0)
                        {
                            rowName = reader.IsDBNull(i) ? "" : reader.GetValue(i)?.ToString() ?? "";
                            continue;
                        }
                        if (!IsNumeric(reader.GetFieldType(i))) continue;
                        columnName = reader.GetName(i);
                        gaugeName = GaugeNaming.Build(_cfg.DisplayHostname, _q.IncludeHostname, _q.Prefix, rowName, columnName);
                    }
                    else
                    {
                        if (!IsNumeric(reader.GetFieldType(i))) continue;
                        columnName = reader.GetName(i);
                        gaugeName = GaugeNaming.Build(_cfg.DisplayHostname, _q.IncludeHostname, _q.Prefix, null, columnName);
                    }

                    var value = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false)
                        ? 0d
                        : ToDouble(await reader.GetFieldValueAsync<object>(i, ct).ConfigureAwait(false));

                    GetGauge(gaugeName, columnName).WithLabels(hostLabel, _cfg.DriverLabel).Set(value);
                }
                if (!_q.MultiRow) break; // single-row: first row only
            }

            _numCollections++;
            Interlocked.Exchange(ref _lastSuccessTimestamp, Environment.TickCount64);
            if (_numCollections == 1) StartIntervalLoop();
        }
        catch (Exception e)
        {
            _log.LogError("ERROR!! ABORTING COLLECTION!! Cause: {Cause}", e.Message);
            _log.LogDebug("{Exception}", e.ToString());
            // Java parity: unregister this query's metrics; a fresh registry
            // means the whole family disappears until the next successful gather.
            _gauges.Clear();
            ResetRegistry();
        }
        finally
        {
            _gatherLock.Release();
        }
    }

    private void ResetRegistry()
    {
        _registry = Metrics.NewCustomRegistry();
        _factory = Metrics.WithCustomRegistry(_registry);
    }

    private Gauge GetGauge(string name, string help)
    {
        if (_gauges.TryGetValue(name, out var existing)) return existing;
        _log.LogDebug("registering gauge: {Name}", name);
        var gauge = _factory.CreateGauge(name, help, LabelNames);
        _gauges[name] = gauge;
        return gauge;
    }

    private void StartIntervalLoop()
    {
        if (_q.IntervalSeconds <= 0 || _q.IntervalSeconds >= ExporterConfig.InfiniteInterval)
            return; // on-demand only ("interval defaults to infinity")

        _intervalLoop = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_q.IntervalSeconds));
            try
            {
                // Java parity: sleep first, then gather.
                while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                {
                    try { await GatherAsync(throwOnConnectError: false, _cts.Token).ConfigureAwait(false); }
                    catch (Exception e) { _log.LogError("interval gather failed: {Message}", e.Message); }
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    internal static bool IsNumeric(Type t) =>
        t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) ||
        t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte) ||
        t == typeof(float) || t == typeof(double) || t == typeof(decimal);

    internal static double ToDouble(object v) => v switch
    {
        double d => d,
        float f => f,
        decimal m => (double)m,
        long l => l,
        int i => i,
        IConvertible c => c.ToDouble(CultureInfo.InvariantCulture),
        _ => 0d,
    };

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_intervalLoop != null)
        {
            try { await _intervalLoop.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        _gatherLock.Dispose();
    }
}
