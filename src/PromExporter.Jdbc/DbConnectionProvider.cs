using System.Data.Common;
using System.Reflection;

namespace PromExporter.Jdbc;

public interface IDbConnectionProvider
{
    /// <summary>Return an open connection, (re)connecting if needed. Throws if the DB is unreachable.</summary>
    Task<DbConnection> GetOpenConnectionAsync(CancellationToken ct = default);
}

/// <summary>
/// One connection per collector (Java parity: a ConnectionManager per
/// populator), reconnect-on-demand. Parallel gathers (e.g. Task.WhenAll on
/// /metrics_now) therefore never share a DbConnection.
/// </summary>
public sealed class DbConnectionProvider : IDbConnectionProvider, IDisposable
{
    private readonly ExporterConfig _cfg;
    private readonly ILogger _log;
    private DbProviderFactory? _factory;
    private DbConnection? _connection;

    public DbConnectionProvider(ExporterConfig cfg, ILogger log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<DbConnection> GetOpenConnectionAsync(CancellationToken ct = default)
    {
        if (_connection is { State: System.Data.ConnectionState.Open })
            return _connection;

        _connection?.Dispose();
        _connection = null;

        var factory = ResolveFactory();
        var conn = factory.CreateConnection()
            ?? throw new InvalidOperationException($"Provider '{_cfg.Provider}' returned no DbConnection");
        conn.ConnectionString = _cfg.BuildConnectionString();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        _log.LogDebug("Opened {Provider} connection", _cfg.Provider);
        return _connection = conn;
    }

    private DbProviderFactory ResolveFactory() =>
        _factory ??= _cfg.Provider switch
        {
            "odbc" => System.Data.Odbc.OdbcFactory.Instance,
            "sqlite" => Microsoft.Data.Sqlite.SqliteFactory.Instance,
            "custom" => LoadCustomFactory(),
            _ => throw new InvalidOperationException($"Unknown provider '{_cfg.Provider}' (expected odbc|sqlite|custom/db2)"),
        };

    private DbProviderFactory LoadCustomFactory()
    {
        if (string.IsNullOrEmpty(_cfg.DriverAssembly) || string.IsNullOrEmpty(_cfg.DriverFactory))
            throw new InvalidOperationException("provider 'custom' requires both 'driver_assembly' and 'driver_factory'");

        var asm = Assembly.LoadFrom(_cfg.DriverAssembly!);
        var type = asm.GetType(_cfg.DriverFactory!, throwOnError: false)
            ?? throw new InvalidOperationException($"Type '{_cfg.DriverFactory}' not found in '{_cfg.DriverAssembly}'");

        // Conventional static Instance (IBM.Data.DB2.DB2ProviderFactory et al.)...
        var inst = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                   ?? type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        return inst as DbProviderFactory
               ?? (Activator.CreateInstance(type) as DbProviderFactory)
               ?? throw new InvalidOperationException($"'{_cfg.DriverFactory}' is not a DbProviderFactory");
    }

    public void Dispose() => _connection?.Dispose();
}
