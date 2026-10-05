using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace PromExporter.Jdbc.Tests;

public class QueryCollectorTests
{
    private static ExporterConfig Cfg(string queriesJson)
        => TestConfig.FromJson(TestConfig.Base.Replace("\"queries\": []", "\"queries\": " + queriesJson));

    private static async Task<string> ExportAsync(QueryCollector c)
    {
        var ms = new MemoryStream();
        await c.ExportAsync(ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public async Task SingleRow_exportsNumericColumns_firstRowOnly()
    {
        var db = new FakeDb();
        db.Connection.Script["SELECT * FROM stats"] = FakeOutcome.Of(
            new FakeResult(["NAME", "CNT", "RATE", "NOTE"],
                           [typeof(string), typeof(int), typeof(double), typeof(string)],
                            [
                                new object?[] { "row1", 42, 1.5, "text" },
                                new object?[] { "row2", 99, 9.9, "text2" }, // ignored: single-row mode
                            ]));
        var cfg = Cfg("""[{"sql": "SELECT * FROM stats", "prefix": "PFX"}]""");
        await using var c = new QueryCollector(db, cfg, cfg.Queries[0], NullLogger.Instance);

        await c.GatherNowAsync(0);
        var text = await ExportAsync(c);

        Assert.Contains("# TYPE PFX__CNT gauge", text);
        Assert.Contains("PFX__CNT{hostname=\"h1\",driver_class=\"sqlite\"} 42", text);
        Assert.Contains("PFX__RATE{hostname=\"h1\",driver_class=\"sqlite\"} 1.5", text);
        Assert.DoesNotContain("PFX__NOTE", text);   // non-numeric skipped
        Assert.DoesNotContain("PFX__NAME", text);
        Assert.DoesNotContain("99", text);          // second row ignored
    }

    [Fact]
    public async Task SingleRow_nullValue_exportsZero()
    {
        var db = new FakeDb();
        db.Connection.Script["SELECT * FROM t"] = FakeOutcome.Of(
            new FakeResult(["CNT"], [typeof(int)], [new object?[] { null }]));
        var cfg = Cfg("""[{"sql": "SELECT * FROM t"}]""");
        await using var c = new QueryCollector(db, cfg, cfg.Queries[0], NullLogger.Instance);

        await c.GatherNowAsync(0);
        Assert.Contains("CNT{hostname=\"h1\",driver_class=\"sqlite\"} 0", await ExportAsync(c));
    }

    [Fact]
    public async Task MultiRow_firstColumnNamesTheRow()
    {
        var db = new FakeDb();
        db.Connection.Script["SELECT * FROM pools"] = FakeOutcome.Of(
            new FakeResult(["POOL", "SIZE", "USED"],
                           [typeof(string), typeof(long), typeof(long)],
                            [
                                new object?[] { "QWORK", 100, 60 },
                                new object?[] { "QINTER", 200, 20 },
                            ]));
        var cfg = Cfg("""[{"sql": "SELECT * FROM pools", "multi_row": true, "prefix": "MEMPOOL"}]""");
        await using var c = new QueryCollector(db, cfg, cfg.Queries[0], NullLogger.Instance);

        await c.GatherNowAsync(0);
        var text = await ExportAsync(c);
        Assert.Contains("MEMPOOL__QWORK__SIZE{hostname=\"h1\",driver_class=\"sqlite\"} 100", text);
        Assert.Contains("MEMPOOL__QINTER__SIZE{hostname=\"h1\",driver_class=\"sqlite\"} 200", text);
        Assert.Contains("MEMPOOL__QINTER__USED{hostname=\"h1\",driver_class=\"sqlite\"} 20", text);
    }

    [Fact]
    public async Task SetupStatements_runBeforeMain_inOrder()
    {
        var db = new FakeDb();
        db.Connection.Script["CALL P('x')"] = FakeOutcome.NonQuery();
        db.Connection.Script["INSERT INTO t VALUES(1)"] = FakeOutcome.NonQuery();
        db.Connection.Script["SELECT CNT FROM t"] = FakeOutcome.Of(
            new FakeResult(["CNT"], [typeof(int)], [new object?[] { 7 }]));
        var cfg = Cfg("""[{"sql": "CALL P('x'); INSERT INTO t VALUES(1); SELECT CNT FROM t"}]""");
        await using var c = new QueryCollector(db, cfg, cfg.Queries[0], NullLogger.Instance);

        await c.GatherNowAsync(0);
        Assert.Equal(["CALL P('x')", "INSERT INTO t VALUES(1)", "SELECT CNT FROM t"], db.Connection.Executed);
        Assert.Contains("CNT{hostname=\"h1\",driver_class=\"sqlite\"} 7", await ExportAsync(c));
    }

    [Fact]
    public async Task ErrorDuringGather_metricsVanish_thenRecover()
    {
        var db = new FakeDb();
        var ok = FakeOutcome.Of(new FakeResult(["CNT"], [typeof(int)], [new object?[] { 7 }]));
        db.Connection.Script["SELECT CNT FROM t"] = ok;
        var cfg = Cfg("""[{"sql": "SELECT CNT FROM t"}]""");
        await using var c = new QueryCollector(db, cfg, cfg.Queries[0], NullLogger.Instance);

        await c.GatherNowAsync(0);
        Assert.Contains("CNT{", await ExportAsync(c));

        // Statement-level failure -> this collector's whole family disappears (Java parity:
        // unregister on error) and returns as soon as a gather succeeds again.
        db.Connection.Script["SELECT CNT FROM t"] = FakeOutcome.Fail(new Exception("db2 boom"));
        await c.GatherNowAsync(0);
        Assert.Equal("", await ExportAsync(c));

        db.Connection.Script["SELECT CNT FROM t"] = ok;
        await c.GatherNowAsync(0);
        Assert.Contains("CNT{hostname=\"h1\",driver_class=\"sqlite\"} 7", await ExportAsync(c));
    }

    [Fact]
    public async Task GatherNow_withinTolerance_skips()
    {
        var db = new FakeDb();
        db.Connection.Script["SELECT CNT FROM t"] = FakeOutcome.Of(
            new FakeResult(["CNT"], [typeof(int)], [new object?[] { 7 }]));
        var cfg = Cfg("""[{"sql": "SELECT CNT FROM t"}]""");
        await using var c = new QueryCollector(db, cfg, cfg.Queries[0], NullLogger.Instance);

        await c.GatherNowAsync(0);
        await c.GatherNowAsync(5000); // /metrics_now semantics: recent success -> no re-run
        Assert.Single(db.Connection.Executed);
    }

    [Fact]
    public async Task FirstGather_connectFailure_throws()
    {
        var db = new FakeDb { FailConnect = true };
        var cfg = Cfg("""[{"sql": "SELECT 1 FROM t"}]""");
        await using var c = new QueryCollector(db, cfg, cfg.Queries[0], NullLogger.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.GatherNowAsync(0));
    }

    [Fact]
    public async Task IntervalLoop_gathersAfterInterval()
    {
        var db = new FakeDb();
        db.Connection.Script["SELECT CNT FROM t"] = FakeOutcome.Of(
            new FakeResult(["CNT"], [typeof(int)], [new object?[] { 1 }]));
        var cfg = Cfg("""[{"sql": "SELECT CNT FROM t", "interval": 1}]""");
        await using var c = new QueryCollector(db, cfg, cfg.Queries[0], NullLogger.Instance);

        await c.GatherNowAsync(0);
        Assert.Single(db.Connection.Executed); // only the verification gather

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (db.Connection.Executed.Count < 2 && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        Assert.Equal(2, db.Connection.Executed.Count); // interval thread fired once
    }

    [Fact]
    public void IsNumeric_matchesJavaJdbcTypes()
    {
        foreach (var t in new[] { typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int),
                                  typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) })
            Assert.True(QueryCollector.IsNumeric(t), t.Name);
        foreach (var t in new[] { typeof(string), typeof(DateTime), typeof(bool), typeof(Guid), typeof(object) })
            Assert.False(QueryCollector.IsNumeric(t), t.Name);
    }
}
