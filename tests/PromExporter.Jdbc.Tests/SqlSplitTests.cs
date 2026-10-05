namespace PromExporter.Jdbc.Tests;

public class SqlSplitTests
{
    [Fact]
    public void NoDelimiter_singleMainQuery()
    {
        var (setup, main) = QueryCollector.SplitSql("SELECT 1");
        Assert.Empty(setup);
        Assert.Equal("SELECT 1", main);
    }

    [Fact]
    public void OneSetupStatement_lastPartIsMain()
    {
        var (setup, main) = QueryCollector.SplitSql("CALL P('a'); SELECT x FROM t");
        Assert.Equal(["CALL P('a')"], setup);
        Assert.Equal("SELECT x FROM t", main);
    }

    [Fact]
    public void MultipleSetupStatements_orderPreserved()
    {
        var (setup, main) = QueryCollector.SplitSql("S1; S2; S3; MAIN");
        Assert.Equal(["S1", "S2", "S3"], setup);
        Assert.Equal("MAIN", main);
    }

    [Fact]
    public void DelimiterInsideSql_withoutSpace_staysInMain()
    {
        // Java parity: dump-split on the literal "; " only.
        var (setup, main) = QueryCollector.SplitSql("SELECT 1;SELECT 2");
        Assert.Empty(setup);
        Assert.Equal("SELECT 1;SELECT 2", main);
    }
}
