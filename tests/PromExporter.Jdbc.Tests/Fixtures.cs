namespace PromExporter.Jdbc.Tests;

/// <summary>Process-wide env mutation, restored on dispose.</summary>
public sealed class EnvScope : IDisposable
{
    private readonly List<(string, string?)> _restore = [];

    public EnvScope Set(string name, string? value)
    {
        _restore.Add((name, Environment.GetEnvironmentVariable(name)));
        Environment.SetEnvironmentVariable(name, value);
        return this;
    }

    public void Dispose()
    {
        foreach (var (name, value) in _restore)
            Environment.SetEnvironmentVariable(name, value);
        _restore.Clear();
    }
}

public sealed class FakeConsoleInteraction : IConsoleInteraction
{
    public bool NonInteractive { get; init; } = true;
    public Func<string, string?>? OnAsk { get; init; }
    public string? AskLine(string prompt) => OnAsk?.Invoke(prompt);
    public bool ConfirmYesNo(string prompt, bool defaultValue) => defaultValue;
}

public static class TestConfig
{
    public static ExporterConfig FromJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "promexporter-test-" + Guid.NewGuid() + ".json");
        File.WriteAllText(path, json);
        var cfg = ExporterConfig.Load(path, new FakeConsoleInteraction(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        File.Delete(path);
        return cfg;
    }

    public const string Base = """
    {
      "port": 9999,
      "provider": "sqlite",
      "hostname": "h1",
      "username": "u",
      "password": "p",
      "queries": []
    }
    """;
}
