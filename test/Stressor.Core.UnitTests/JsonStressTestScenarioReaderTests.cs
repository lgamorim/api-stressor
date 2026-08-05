namespace Stressor.Core.UnitTests;

public class JsonStressTestScenarioReaderTests
{
    private readonly JsonStressTestScenarioReader _reader = new();

    [Fact]
    public async Task Should_ReadAllFields_When_ValidDocument()
    {
        var path = await WriteTempScenarioAsync(
            """
            {
              "url": "https://example.com/orders",
              "payload": "./payload.json",
              "method": "PUT",
              "requests": 10,
              "interval": "1s",
              "cycles": 60,
              "auth": "Bearer token",
              "verbose": "failures",
              "load": "batch",
              "batch": 5,
              "timeout": "30s",
              "cycleInterval": "10s"
            }
            """);

        var document = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Equal("https://example.com/orders", document.Url);
        Assert.Equal("./payload.json", document.Payload);
        Assert.Equal("PUT", document.Method);
        Assert.Equal(10, document.Requests);
        Assert.Equal("1s", document.Interval);
        Assert.Equal(60, document.Cycles);
        Assert.Equal("Bearer token", document.Auth);
        Assert.Equal("failures", document.Verbose);
        Assert.Equal("batch", document.Load);
        Assert.Equal(5, document.Batch);
        Assert.Equal("30s", document.Timeout);
        Assert.Equal("10s", document.CycleInterval);
    }

    [Fact]
    public async Task Should_Throw_When_FileNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _reader.ReadAsync(path, TestCancellation.Token));
    }

    [Fact]
    public async Task Should_Throw_When_InvalidJson()
    {
        var path = await WriteTempScenarioAsync("{ not json");

        var exception = await Assert.ThrowsAsync<StressTestScenarioValidationException>(() =>
            _reader.ReadAsync(path, TestCancellation.Token));

        Assert.Contains("valid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Throw_When_EmptyFile()
    {
        var path = await WriteTempScenarioAsync("   ");

        var exception = await Assert.ThrowsAsync<StressTestScenarioValidationException>(() =>
            _reader.ReadAsync(path, TestCancellation.Token));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> WriteTempScenarioAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(path, content, TestCancellation.Token);
        return path;
    }
}
