namespace Stressor.Core.UnitTests;

using System.Text.Json;

public class JsonSessionReportWriterTests
{
    [Fact]
    public async Task Should_WriteValidJson_When_ReportProvided()
    {
        var reportPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "report.json");
        var writer = new JsonSessionReportWriter(TimeProvider.System);
        var options = CreateOptions() with
        {
            Auth = "Bearer secret",
            Headers = new Dictionary<string, string> { ["X-Api-Key"] = "abc123" }
        };
        var report = new SessionReport(
            options,
            [new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(45), null)],
            false);

        await writer.WriteAsync(report, reportPath, 0, "1.0.0-beta", TestCancellation.Token);

        var json = await File.ReadAllTextAsync(reportPath, TestCancellation.Token);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("1.0.0-beta", document.RootElement.GetProperty("stressorVersion").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("exitCode").GetInt32());
        Assert.False(document.RootElement.GetProperty("wasCancelled").GetBoolean());
        Assert.DoesNotContain("secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_CreateParentDirectory_When_Missing()
    {
        var reportPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nested", "report.json");
        var writer = new JsonSessionReportWriter(TimeProvider.System);
        var report = new SessionReport(CreateOptions(), [], false);

        await writer.WriteAsync(report, reportPath, 0, "1.0.0-beta", TestCancellation.Token);

        Assert.True(File.Exists(reportPath));
    }

    [Fact]
    public async Task Should_OverwriteExistingFile_When_PathExists()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var reportPath = Path.Combine(directory, "report.json");
        await File.WriteAllTextAsync(reportPath, "old", TestCancellation.Token);

        var writer = new JsonSessionReportWriter(TimeProvider.System);
        var report = new SessionReport(
            CreateOptions(),
            [new RequestOutcome(1, 1, false, false, 500, TimeSpan.Zero, "error")],
            false);

        await writer.WriteAsync(report, reportPath, 1, "1.0.0-beta", TestCancellation.Token);

        var json = await File.ReadAllTextAsync(reportPath, TestCancellation.Token);
        Assert.Contains("\"exitCode\": 1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("old", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_PathEmpty()
    {
        var writer = new JsonSessionReportWriter(TimeProvider.System);
        var report = new SessionReport(CreateOptions(), [], false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            writer.WriteAsync(report, "  ", 0, "1.0.0-beta", TestCancellation.Token));
    }

    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, 1, TimeSpan.FromSeconds(1), 1);
}
