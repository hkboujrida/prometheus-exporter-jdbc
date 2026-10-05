using System.Text.Json;

namespace PromExporter.Jdbc;

public sealed record SqlQueryConfig(
    string Sql,
    string? Name,
    long IntervalSeconds,
    string? Prefix,
    bool IncludeHostname,
    bool MultiRow);

/// <summary>Interactive/non-interactive console interaction, injectable for tests.</summary>
public interface IConsoleInteraction
{
    bool NonInteractive { get; }
    string? AskLine(string prompt);
    bool ConfirmYesNo(string prompt, bool defaultValue);
}

public sealed class ConsoleInteraction : IConsoleInteraction
{
    public bool NonInteractive =>
        Truthy(Environment.GetEnvironmentVariable("PROMCLIENT_NONINTERACTIVE")) ||
        Console.IsInputRedirected;

    public string? AskLine(string prompt)
    {
        if (NonInteractive) return null;
        Console.Write(prompt);
        return Console.ReadLine();
    }

    public bool ConfirmYesNo(string prompt, bool defaultValue)
    {
        if (NonInteractive) return defaultValue;
        Console.Write(prompt);
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(answer) ? defaultValue : answer is "y" or "yes";
    }

    internal static bool Truthy(string? v) =>
        v is not null && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// config.json model + resolution order, mirroring the Java exporter's Config:
/// explicit value -> env (HOSTNAME/USERNAME/PASSWORD/PORT) -> prompt/default.
/// Port precedence: PORT env -> PROMCLIENT_PORT env -> JSON port -> 9853.
/// </summary>
public sealed class ExporterConfig
{
    public const int DefaultPort = 9853;
    public const long InfiniteInterval = int.MaxValue;

    private readonly JsonElement _root;
    private readonly string _path;
    private readonly IConsoleInteraction _io;
    private readonly ILogger _log;
    private readonly List<string> _warnings = [];

    public IReadOnlyList<SqlQueryConfig> Queries { get; }
    public int Port { get; }
    public string Hostname { get; private set; }
    public string Username { get; private set; }
    public string Password { get; private set; }
    public string DisplayHostname { get; }

    /// <summary>One of odbc|sqlite|custom (db2 is an alias for custom).</summary>
    public string Provider { get; }
    public string? ConnectionString { get; }
    public string OdbcDriver { get; }
    public string? DriverAssembly { get; }
    public string? DriverFactory { get; }

    /// <summary>Value of the legacy/exposition 'driver_class' gauge label.</summary>
    public string DriverLabel { get; }

    public IReadOnlyList<string> Warnings => _warnings;


    public static ExporterConfig Load(string path, IConsoleInteraction io, ILogger log)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        return new ExporterConfig(doc.RootElement.Clone(), path, io, log);
    }

    private ExporterConfig(JsonElement root, string path, IConsoleInteraction io, ILogger log)
    {
        _root = root;
        _path = path;
        _io = io;
        _log = log;

        Queries = ReadQueries(root);
        Port = ResolvePort(root, log);
        Hostname = ResolveHost(root);
        Username = ResolveUser(root);
        Password = ResolvePassword(root);
        DisplayHostname = root.TryGetString("hostname", out var h) ? h! : Hostname;

        Provider = (root.TryGetString("provider", out var p) ? p : "odbc")!.Trim().ToLowerInvariant();
        if (Provider == "db2") Provider = "custom";

        // Legacy JDBC keys.
        if (root.TryGetString("driver_class", out var dc) && !string.IsNullOrEmpty(dc))
        {
            _warnings.Add($"Ignoring legacy JDBC 'driver_class' ({dc}); it is used only as the driver_class metric label. " +
                          "Configure 'provider'/'driver_assembly'/'driver_factory' instead.");
            DriverLabel = dc!;
        }
        else
        {
            DriverLabel = root.TryGetString("driver_factory", out var df) && !string.IsNullOrEmpty(df) ? df! : Provider;
        }

        ConnectionString =
            root.TryGetString("connection_string", out var cs) && !string.IsNullOrEmpty(cs) ? cs
            : root.TryGetString("driver_uri", out var du) && !string.IsNullOrEmpty(du) ? du   // legacy key
            : null;

        OdbcDriver = root.TryGetString("odbc_driver", out var od) && !string.IsNullOrEmpty(od)
            ? od! : "IBM i Access ODBC Driver";
        DriverAssembly = root.TryGetString("driver_assembly", out var da) ? da : null;
        DriverFactory = root.TryGetString("driver_factory", out var dfa) ? dfa : null;

        foreach (var w in _warnings) log.LogWarning("{Warning}", w);
    }

    public string BuildConnectionString()
    {
        if (!string.IsNullOrEmpty(ConnectionString))
            return ConnectionString;

        return Provider switch
        {
            "odbc" => BuildOdbc(),
            "sqlite" => $"Data Source={Hostname}",
            // Parity with the Java exporter's quirk: with no driver_uri the
            // hostname itself was used as the connection URL.
            "custom" when !string.IsNullOrEmpty(Hostname) => Hostname,
            _ => throw new InvalidOperationException($"provider '{Provider}' requires an explicit connection_string"),
        };

        string BuildOdbc()
        {
            var s = $"DRIVER={{{OdbcDriver}}};SYSTEM={Hostname};";
            if (!string.IsNullOrEmpty(Username)) s += $"UID={Username};";
            if (!string.IsNullOrEmpty(Password)) s += $"PWD={Password};";
            return s;
        }
    }

    private List<SqlQueryConfig> ReadQueries(JsonElement root)
    {
        var ret = new List<SqlQueryConfig>();
        if (!root.TryGetProperty("queries", out var queries) || queries.ValueKind != JsonValueKind.Array)
        {
            _log.LogError("No queries found in config file {Path}", _path);
            return ret;
        }

        foreach (var q in queries.EnumerateArray())
        {
            if (q.ValueKind != JsonValueKind.Object) continue;
            if (q.TryGetProperty("enabled", out var enabled) && !TruthyOrTrue(enabled)) continue;

            var sql = q.TryGetString("sql", out var s) ? s : null;
            if (string.IsNullOrEmpty(sql))
            {
                _log.LogError("No SQL found for query in config file {Path}", _path);
                continue;
            }

            long interval = InfiniteInterval;
            if (q.TryGetProperty("interval", out var iv) && iv.ValueKind == JsonValueKind.Number)
                interval = iv.GetInt64();
            else
                _log.LogDebug("No interval found for query '{Sql}'; using on-demand collection only", sql);

            var multiRow = q.TryGetProperty("multi_row", out var mr) && TruthyOrTrue(mr);
            var includeHostname = q.TryGetProperty("include_hostname", out var ih) && TruthyOrTrue(ih);
            var prefix = q.TryGetString("prefix", out var px) ? px : null;
            var name = q.TryGetString("name", out var n) ? n : null;

            ret.Add(new SqlQueryConfig(sql, name, interval, prefix, includeHostname, multiRow));
        }
        return ret;
    }

    private static bool TruthyOrTrue(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => e.GetString()!.Equals("true", StringComparison.OrdinalIgnoreCase),
        _ => true,
    };

    private int ResolvePort(JsonElement root, ILogger log)
    {
        var envPort = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrEmpty(envPort))
        {
            if (int.TryParse(envPort, out var p)) return p;
            log.LogWarning("Invalid port number in PORT environment variable: {Value}", envPort);
        }
        var propPort = Environment.GetEnvironmentVariable("PROMCLIENT_PORT");
        if (!string.IsNullOrEmpty(propPort))
        {
            if (int.TryParse(propPort, out var p)) return p;
            log.LogWarning("Invalid port number in PROMCLIENT_PORT: {Value}", propPort);
        }
        if (root.TryGetProperty("port", out var jp) && jp.ValueKind == JsonValueKind.Number)
            return jp.GetInt32();
        log.LogError("Unable to use configured port. Falling back to tool default");
        return DefaultPort;
    }

    private string ResolveHost(JsonElement root)
    {
        if (root.TryGetString("hostname", out var h) && !string.IsNullOrEmpty(h)) return h!;
        var env = Environment.GetEnvironmentVariable("HOSTNAME");
        if (!string.IsNullOrEmpty(env)) return env;
        var answer = _io.AskLine("Enter system name: ");
        if (!string.IsNullOrEmpty(answer)) return answer;
        throw new InvalidOperationException("No hostname configured: set 'hostname' in config, the HOSTNAME env var, run interactively, or set PROMCLIENT_NONINTERACTIVE=0 with a TTY.");
    }

    private string ResolveUser(JsonElement root)
    {
        if (root.TryGetString("username", out var u) && !string.IsNullOrEmpty(u)) return u!;
        var env = Environment.GetEnvironmentVariable("USERNAME");
        if (!string.IsNullOrEmpty(env)) return env;
        var answer = _io.AskLine("Username:");
        if (!string.IsNullOrEmpty(answer)) return answer;
        throw new InvalidOperationException("No username configured: set 'username' in config or the USERNAME env var.");
    }

    private string ResolvePassword(JsonElement root)
    {
        if (root.TryGetString("password", out var p) && !string.IsNullOrEmpty(p))
        {
            _warnings.Add($"Password is stored in config file {_path}. THIS IS NOT SECURE!");
            return p!;
        }
        var env = Environment.GetEnvironmentVariable("PASSWORD");
        if (!string.IsNullOrEmpty(env)) return env;
        var answer = _io.AskLine("Password: ");
        return answer ?? string.Empty;
    }
}

file static class JsonElementExtensions
{
    public static bool TryGetString(this JsonElement e, string name, out string? value)
    {
        value = null;
        return e.ValueKind == JsonValueKind.Object &&
               e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String &&
               (value = v.GetString()) != null;
    }
}
