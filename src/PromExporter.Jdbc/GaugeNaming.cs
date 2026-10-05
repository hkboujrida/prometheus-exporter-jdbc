using System.Text;

namespace PromExporter.Jdbc;

/// <summary>
/// Prometheus gauge-name construction, mirroring the Java exporter's
/// SQLMetricPopulator.getGaugeName: [hostname__][prefix__][rowname__]column,
/// stripped of everything outside [A-Za-z0-9_], hostname truncated at first '.'.
/// Additionally guarantees a valid leading character, which prometheus-net
/// requires (the Java exporter could emit names starting with a digit).
/// </summary>
public static class GaugeNaming
{
    public static string Build(string? hostname, bool includeHostname, string? prefix, string? rowName, string columnName)
    {
        var sb = new StringBuilder();
        if (includeHostname && !string.IsNullOrEmpty(hostname))
            sb.Append(HostLabel(hostname)).Append("__");
        if (!string.IsNullOrEmpty(prefix))
            sb.Append(prefix).Append("__");
        if (!string.IsNullOrEmpty(rowName))
            sb.Append(rowName).Append("__");
        sb.Append(columnName);
        return Sanitize(sb.ToString());
    }

    /// <summary>Label value for the hostname label: everything before the first dot.</summary>
    public static string HostLabel(string hostname) => hostname.Split('.')[0];

    public static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            if (char.IsAsciiLetterOrDigit(c) || c == '_')
                sb.Append(c);
        if (sb.Length > 0 && char.IsAsciiDigit(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }
}
