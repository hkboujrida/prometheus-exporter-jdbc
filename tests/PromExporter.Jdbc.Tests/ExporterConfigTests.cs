namespace PromExporter.Jdbc.Tests;

// Env-touching tests share one class: xUnit serializes tests within a class.
public class ExporterConfigTests
{
    [Fact]
    public void Port_fromJson()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base);
        Assert.Equal(9999, cfg.Port);
    }

    [Fact]
    public void Port_jsonOverriddenByPromclientPort()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", "1234");
        Assert.Equal(1234, TestConfig.FromJson(TestConfig.Base).Port);
    }

    [Fact]
    public void Port_envOverridesPromclientPort()
    {
        using var env = new EnvScope().Set("PORT", "4321").Set("PROMCLIENT_PORT", "1234");
        Assert.Equal(4321, TestConfig.FromJson(TestConfig.Base).Port);
    }

    [Fact]
    public void Port_invalidEnvFallsThrough()
    {
        using var env = new EnvScope().Set("PORT", "not-a-port").Set("PROMCLIENT_PORT", "1234");
        Assert.Equal(1234, TestConfig.FromJson(TestConfig.Base).Port);
    }

    [Fact]
    public void Port_defaultWhenAbsent()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var json = TestConfig.Base.Replace("\"port\": 9999,", "");
        Assert.Equal(ExporterConfig.DefaultPort, TestConfig.FromJson(json).Port);
    }

    [Fact]
    public void Queries_defaults()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base.Replace(
            "\"queries\": []",
            """
            "queries": [{"sql": "SELECT 1", "name": "n"}]
            """));
        var q = Assert.Single(cfg.Queries);
        Assert.Equal("SELECT 1", q.Sql);
        Assert.Equal(ExporterConfig.InfiniteInterval, q.IntervalSeconds); // default: on-demand only
        Assert.False(q.IncludeHostname);                                   // v1 default is false
        Assert.False(q.MultiRow);
        Assert.Null(q.Prefix);
    }

    [Fact]
    public void Queries_disabledSkipped_emptySqlSkipped()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base.Replace(
            "\"queries\": []",
            """
            "queries": [
              {"sql": "SELECT 1", "enabled": false},
              {"sql": "", "enabled": true},
              {"sql": "SELECT 2"},
              {"sql": "SELECT 3", "enabled": true}
            ]
            """));
        Assert.Equal(["SELECT 2", "SELECT 3"], cfg.Queries.Select(q => q.Sql));
    }

    [Fact]
    public void Queries_fullParse()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base.Replace(
            "\"queries\": []",
            """
            "queries": [{"sql": "SELECT 1", "interval": 30, "prefix": "P", "multi_row": true, "include_hostname": true}]
            """));
        var q = Assert.Single(cfg.Queries);
        Assert.Equal(30, q.IntervalSeconds);
        Assert.Equal("P", q.Prefix);
        Assert.True(q.MultiRow);
        Assert.True(q.IncludeHostname);
    }

    [Fact]
    public void Credentials_fromJson_passwordWarning()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null)
            .Set("HOSTNAME", null).Set("USERNAME", null).Set("PASSWORD", null);
        var cfg = TestConfig.FromJson(TestConfig.Base);
        Assert.Equal("h1", cfg.Hostname);
        Assert.Equal("h1", cfg.DisplayHostname);
        Assert.Equal("u", cfg.Username);
        Assert.Equal("p", cfg.Password);
        Assert.Contains(cfg.Warnings, w => w.Contains("NOT SECURE"));
    }

    [Fact]
    public void Credentials_envFallback()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null)
            .Set("HOSTNAME", "envhost").Set("USERNAME", "envuser").Set("PASSWORD", "envpw");
        var json = TestConfig.Base
            .Replace("\"hostname\": \"h1\",", "").Replace("\"username\": \"u\",", "").Replace("\"password\": \"p\",", "");
        var cfg = TestConfig.FromJson(json);
        Assert.Equal("envhost", cfg.Hostname);
        Assert.Equal("envuser", cfg.Username);
        Assert.Equal("envpw", cfg.Password);
    }

    [Fact]
    public void Credentials_missingHostNonInteractive_throws()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null)
            .Set("HOSTNAME", null).Set("USERNAME", "u").Set("PASSWORD", null);
        var json = TestConfig.Base.Replace("\"hostname\": \"h1\",", "");
        Assert.Throws<InvalidOperationException>(() => TestConfig.FromJson(json));
    }

    [Fact]
    public void Odbc_connectionStringBuiltFromParts()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base.Replace("\"provider\": \"sqlite\"", "\"provider\": \"odbc\""));
        Assert.Equal("DRIVER={IBM i Access ODBC Driver};SYSTEM=h1;UID=u;PWD=p;", cfg.BuildConnectionString());
    }

    [Fact]
    public void Odbc_customDriverName()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base
            .Replace("\"provider\": \"sqlite\"", "\"provider\": \"odbc\", \"odbc_driver\": \"My Driver\""));
        Assert.StartsWith("DRIVER={My Driver};", cfg.BuildConnectionString());
    }

    [Fact]
    public void ConnectionString_explicitWins_overLegacyDriverUri()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base.Replace(
            "\"provider\": \"sqlite\",",
            """
            "provider": "custom", "driver_uri": "jdbc:ignored", "connection_string": "Data Source=/tmp/t.db",
            """));
        Assert.Equal("Data Source=/tmp/t.db", cfg.BuildConnectionString());
    }

    [Fact]
    public void Legacy_driverUri_becomesConnectionString_andDriverClassWarns()
    {
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base.Replace(
            "\"provider\": \"sqlite\",",
            """
            "provider": "custom", "driver_uri": "file:/tmp/x.db", "driver_class": "com.ibm.as400.access.AS400JDBCDriver", "driver_assembly": "a.dll", "driver_factory": "F",
            """));
        Assert.Equal("file:/tmp/x.db", cfg.BuildConnectionString());
        Assert.Equal("com.ibm.as400.access.AS400JDBCDriver", cfg.DriverLabel); // label parity with Java
        Assert.Contains(cfg.Warnings, w => w.Contains("legacy JDBC"));
    }

    [Fact]
    public void Custom_withoutConnectionString_fallsBackToHostname()
    {
        // Java parity quirk: hostname doubles as the connection URL.
        using var env = new EnvScope().Set("PORT", null).Set("PROMCLIENT_PORT", null);
        var cfg = TestConfig.FromJson(TestConfig.Base.Replace(
            "\"provider\": \"sqlite\",",
            """
            "provider": "custom", "driver_assembly": "a.dll", "driver_factory": "F",
            """));
        Assert.Equal("h1", cfg.BuildConnectionString());
    }

    [Fact]
    public void Sqlite_connectionString_isDataSource()
        => Assert.Equal("Data Source=h1", TestConfig.FromJson(TestConfig.Base).BuildConnectionString());

    [Fact]
    public void Db2Alias_mapsToCustom()
        => Assert.Equal("custom", TestConfig.FromJson(
            TestConfig.Base.Replace("\"provider\": \"sqlite\"", "\"provider\": \"db2\"")).Provider);
}
