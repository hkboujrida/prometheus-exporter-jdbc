using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using PromExporter.Jdbc;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var verbose = args.Contains("--verbose") || ConsoleInteraction.Truthy(Environment.GetEnvironmentVariable("PROMCLIENT_VERBOSE"));

        using var loggerFactory = LoggerFactory.Create(b => b
            .AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            })
            .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information)
            .AddFilter("Microsoft.AspNetCore", LogLevel.Warning));
        var log = loggerFactory.CreateLogger("PromExporter");

        if (args.Contains("sc"))
            return RunServiceCommander(log);

        var io = new ConsoleInteraction();

        string? configPath =
            ParseFlag(args, "--config")
            ?? Environment.GetEnvironmentVariable("PROMCLIENT_CONFIG");
        var explicitPath = configPath is not null;
        configPath ??= Path.Combine(Directory.GetCurrentDirectory(), "config.json");

        if (!File.Exists(configPath))
        {
            if (!explicitPath && !io.NonInteractive &&
                io.ConfirmYesNo($"Configuration file {configPath} not found. Would you like to initialize one with defaults? [y] ", true))
            {
                await File.WriteAllTextAsync(configPath, Resource("default-config.json"));
                Console.WriteLine($"Wrote default configuration to {configPath}");
            }
            else
            {
                Console.Error.WriteLine($"Configuration file not found: {configPath} " +
                                        "(create it, pass --config, or set PROMCLIENT_CONFIG)");
                return 1;
            }
        }

        ExporterConfig cfg;
        try
        {
            cfg = ExporterConfig.Load(configPath, io, log);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"ERROR: {e.Message}");
            return 1;
        }

        var connProviders = new List<DbConnectionProvider>();
        DbConnectionProvider MakeProvider()
        {
            var p = new DbConnectionProvider(cfg, log);
            connProviders.Add(p);
            return p;
        }
        var collectors = cfg.Queries
            .Select(q => new QueryCollector(MakeProvider(), cfg, q, log))
            .ToList();

        Console.WriteLine("Verifying metrics collection....");
        try
        {
            foreach (var c in collectors)
                await c.GatherNowAsync(12);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"ERROR: initial metrics collection failed: {e.Message}");
            return 1;
        }
        Console.WriteLine("Metrics collection verified.");

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new PassthroughLoggerProvider(loggerFactory));
        builder.WebHost.UseUrls($"http://0.0.0.0:{cfg.Port}");

        var app = builder.Build();

        async Task<IResult> Scrape()
        {
            var ms = new MemoryStream();
            foreach (var c in collectors)
                await c.ExportAsync(ms);
            return Results.Text(Encoding.UTF8.GetString(ms.ToArray()), "text/plain; version=0.0.4; charset=utf-8");
        }

        app.MapGet("/metrics", Scrape);
        app.MapGet("/", Scrape);
        app.MapGet("/metrics_now", async () =>
        {
            await Task.WhenAll(collectors.Select(async c =>
            {
                try { await c.GatherNowAsync(5000); }
                catch (Exception e) { log.LogError("gather-now failed: {Message}", e.Message); }
            }));
            return await Scrape();
        });

        try
        {
            await app.StartAsync();
        }
        catch (IOException)
        {
            Console.Error.WriteLine($"Port {cfg.Port} is already in use");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("==============================================================");
        Console.WriteLine($"Successfully started Prometheus client on port {cfg.Port}");
        Console.WriteLine("==============================================================");

        await app.WaitForShutdownAsync();
        foreach (var c in collectors) await c.DisposeAsync();
        foreach (var p in connProviders) p.Dispose();
        return 0;
    }

    private static string? ParseFlag(string[] args, string name)
    {
        var a = args.FirstOrDefault(x => x.StartsWith(name + "=", StringComparison.Ordinal));
        return a is null ? null : a[(name.Length + 1)..];
    }

    private static int RunServiceCommander(ILogger log)
    {
        try
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "prometheus.yml");
            File.WriteAllText(path, Resource("sc-prometheus.yml"));
            log.LogInformation("Wrote Service Commander definition to file {Path}", path);
            Console.WriteLine();
            Console.WriteLine("To register with Service Commander, first install Service Commander version 1.5.0 or newer.");
            Console.WriteLine();
            Console.WriteLine("Then run one of:");
            Console.WriteLine($"    ln -sf {path} $HOME/.sc/services/prometheus.yml");
            Console.WriteLine($"    ln -sf {path} /QOpenSys/etc/sc/services/prometheus.yml");
            return 0;
        }
        catch (Exception e)
        {
            log.LogError("Error writing prometheus.yml to file: {Message}", e.Message);
            return -3;
        }
    }

    private static string Resource(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("Resources." + name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded resource '{name}' not found");
        using var stream = asm.GetManifestResourceStream(resName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Keeps ASP.NET Core logging on the exporter's console level (request noise filtered by Warning).</summary>
    private sealed class PassthroughLoggerProvider(ILoggerFactory inner) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => inner.CreateLogger(categoryName);
        public void Dispose() { }
    }
}
