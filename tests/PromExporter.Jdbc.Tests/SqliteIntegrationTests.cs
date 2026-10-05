using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace PromExporter.Jdbc.Tests;

/// <summary>
/// Real-DB end-to-end over Microsoft.Data.Sqlite: proves the wiring
/// ExporterConfig -> DbConnectionProvider -> QueryCollector -> exposition text.
/// One provider per collector (Java parity: a ConnectionManager per populator).
/// </summary>
public class SqliteIntegrationTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    private sealed record Harness(string DbPath, ExporterConfig Config, List<DbConnectionProvider> Providers, List<QueryCollector> Collectors)
        : IAsyncDisposable
    {
        public async Task<string> ScrapeAsync()
        {
            var ms = new MemoryStream();
            foreach (var c in Collectors) await c.ExportAsync(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var c in Collectors) await c.DisposeAsync();
            foreach (var p in Providers) p.Dispose();
            SqliteConnection.ClearAllPools();
            File.Delete(DbPath);
        }
    }

    private static async Task<Harness> StartAsync(string queriesJson)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "promexporter-it-" + Guid.NewGuid() + ".db");
        using (var seedConn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await seedConn.OpenAsync();
            using var cmd = seedConn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE stats(NAME TEXT, CNT INTEGER, RATE REAL, NOTE TEXT);
                INSERT INTO stats VALUES('main', 42, 1.5, 'hello');
                INSERT INTO stats VALUES('two', 99, 9.9, 'ignored');
                CREATE TABLE pools(POOL TEXT, SIZE INTEGER);
                INSERT INTO pools VALUES('QWORK', 100);
                INSERT INTO pools VALUES('QINTER', 200);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var json = TestConfig.Base.Replace("\"queries\": []", "\"queries\": " + queriesJson)
            .Replace("\"hostname\": \"h1\",", $"\"hostname\": \"h1\", \"connection_string\": \"Data Source={dbPath}\",");
        var cfg = TestConfig.FromJson(json);
        var providers = cfg.Queries.Select(_ => new DbConnectionProvider(cfg, NullLogger.Instance)).ToList();
        var collectors = cfg.Queries.Zip(providers)
            .Select(t => new QueryCollector(t.Second, cfg, t.First, NullLogger.Instance)).ToList();
        return new Harness(dbPath, cfg, providers, collectors);
    }

    private static async Task Exec(string dbPath, string sql)
    {
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task EndToEnd_singleMultiAndOnDemandQueries()
    {
        await using var h = await StartAsync("""
        [
          {"sql": "SELECT * FROM stats", "prefix": "STAT"},
          {"sql": "SELECT * FROM pools", "multi_row": true, "prefix": "POOL"}
        ]
        """);
        foreach (var c in h.Collectors) await c.GatherNowAsync(0);
        var text = await h.ScrapeAsync();

        // single-row: numeric columns only, first row only
        Assert.Contains("STAT__CNT{hostname=\"h1\",driver_class=\"sqlite\"} 42", text);
        Assert.Contains("STAT__RATE{hostname=\"h1\",driver_class=\"sqlite\"} 1.5", text);
        Assert.DoesNotContain("STAT__NAME", text);
        Assert.DoesNotContain("STAT__NOTE", text);
        Assert.DoesNotContain("99", text);
        // multi-row: first column names each gauge
        Assert.Contains("POOL__QWORK__SIZE{hostname=\"h1\",driver_class=\"sqlite\"} 100", text);
        Assert.Contains("POOL__QINTER__SIZE{hostname=\"h1\",driver_class=\"sqlite\"} 200", text);
    }

    [Fact]
    public async Task IntervalQuery_picksUpDbUpdatesAndEvictsOnError()
    {
        await using var h = await StartAsync("""
        [
          {"sql": "SELECT * FROM stats", "prefix": "STAT", "interval": 1},
          {"sql": "SELECT * FROM pools", "prefix": "KEEP"}
        ]
        """);
        foreach (var c in h.Collectors) await c.GatherNowAsync(0);

        // value update propagates through the interval loop without any scrape
        await Exec(h.DbPath, "UPDATE stats SET CNT = 77 WHERE NAME='main'");
        await WaitUntilAsync(async () =>
            (await h.ScrapeAsync()).Contains("STAT__CNT{hostname=\"h1\",driver_class=\"sqlite\"} 77"),
            "interval gather to propagate CNT=77");

        // a failing interval gather wipes only that collector's families
        await Exec(h.DbPath, "DROP TABLE stats");
        await WaitUntilAsync(async () =>
            !(await h.ScrapeAsync()).Contains("STAT__CNT{") && (await h.ScrapeAsync()).Contains("KEEP__SIZE{"),
            "error-eviction of the failing collector while the healthy one survives");

        // recovery: recreate data, next interval gather re-registers
        await Exec(h.DbPath, "CREATE TABLE stats(NAME TEXT, CNT INTEGER, RATE REAL, NOTE TEXT); INSERT INTO stats VALUES('main',5,0.5,'x')");
        await WaitUntilAsync(async () =>
            (await h.ScrapeAsync()).Contains("STAT__CNT{hostname=\"h1\",driver_class=\"sqlite\"} 5"),
            "re-registration after DB recovers");
    }

    [Fact]
    public async Task MetricsNow_tolerance_respectsRecentSuccess()
    {
        await using var h = await StartAsync("""[{"sql": "SELECT * FROM stats", "prefix": "STAT"}]""");
        await h.Collectors[0].GatherNowAsync(0);
        await Exec(h.DbPath, "UPDATE stats SET CNT = 77 WHERE NAME='main'");

        await h.Collectors[0].GatherNowAsync(5000); // within tolerance: stale value
        Assert.Contains("STAT__CNT{hostname=\"h1\",driver_class=\"sqlite\"} 42", await h.ScrapeAsync());

        await Task.Delay(5100);
        await h.Collectors[0].GatherNowAsync(5000); // past tolerance: refresh
        Assert.Contains("STAT__CNT{hostname=\"h1\",driver_class=\"sqlite\"} 77", await h.ScrapeAsync());
    }

    [Fact]
    public async Task MetricsNow_parallelGathers_areSafe()
    {
        // Regression for the shared-connection defect: /metrics_now runs all
        // collectors concurrently; each owns its connection so the full
        // document must always be produced.
        await using var h = await StartAsync("""
        [
          {"sql": "SELECT * FROM stats", "prefix": "A"},
          {"sql": "SELECT * FROM pools", "multi_row": true, "prefix": "B"},
          {"sql": "SELECT CNT FROM stats", "prefix": "C"}
        ]
        """);
        for (var round = 0; round < 10; round++)
        {
            await Task.WhenAll(h.Collectors.Select(c => c.GatherNowAsync(0)));
            var text = await h.ScrapeAsync();
            Assert.Contains("A__CNT{", text);
            Assert.Contains("B__QWORK__SIZE{", text);
            Assert.Contains("C__CNT{", text);
        }
    }

    [Fact]
    public async Task SetupStatements_executeEveryGather_beforeMain()
    {
        await using var h = await StartAsync("""
        [{"sql": "CREATE TABLE IF NOT EXISTS scratch(SEQ INTEGER); DELETE FROM scratch; INSERT INTO scratch VALUES(7); SELECT SEQ AS CNT FROM scratch", "prefix": "SCR"}]
        """);
        await h.Collectors[0].GatherNowAsync(0);
        Assert.Contains("SCR__CNT{hostname=\"h1\",driver_class=\"sqlite\"} 7", await h.ScrapeAsync());
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string what)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < Deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail("Timed out waiting for " + what);
    }
}
