namespace PromExporter.Jdbc.Tests;

public class GaugeNamingTests
{
    [Theory]
    // (hostname, includeHostname, prefix, rowName, column, expected)
    [InlineData("sys1", false, null, null, "CNT", "CNT")]
    [InlineData("sys1.domain.com", false, "PFX", null, "CNT", "PFX__CNT")]
    [InlineData("sys1.domain.com", true, "PFX", null, "CNT", "sys1__PFX__CNT")]
    [InlineData("sys1", false, "PFX", "POOL_A", "CUR_SIZE", "PFX__POOL_A__CUR_SIZE")]
    [InlineData("sys1", true, null, "R1", "V", "sys1__R1__V")]
    [InlineData("sys1", false, "P-F.X", null, "A B", "PFX__AB")]           // strip invalid chars
    [InlineData("sys1", false, null, null, "9LIVES", "_9LIVES")]           // leading digit fixed for prometheus-net
    [InlineData("sys1", true, null, null, "CNT", "sys1__CNT")]
    [InlineData("", true, null, null, "CNT", "CNT")]                        // empty hostname contributes nothing
    public void Build_matches_java_exporter_format(string hostname, bool includeHostname, string? prefix, string? rowName, string column, string expected)
        => Assert.Equal(expected, GaugeNaming.Build(hostname, includeHostname, prefix, rowName, column));

    [Theory]
    [InlineData("sys1", "sys1")]
    [InlineData("sys1.domain.com", "sys1")]
    [InlineData("", "")]
    public void HostLabel_truncatesAtFirstDot(string host, string expected)
        => Assert.Equal(expected, GaugeNaming.HostLabel(host));

    [Theory]
    [InlineData("ABC", "ABC")]
    [InlineData("a-b c", "abc")]
    [InlineData("_1x", "_1x")]
    [InlineData("3x", "_3x")]
    public void Sanitize(string input, string expected)
        => Assert.Equal(expected, GaugeNaming.Sanitize(input));
}
