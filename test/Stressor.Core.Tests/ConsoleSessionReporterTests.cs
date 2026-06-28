namespace Stressor.Core.Tests;

using System.Globalization;

public class ConsoleSessionReporterTests
{
    [Fact]
    public void WriteSessionComplete_FullSuccessSession_ContainsUrlMethodAndSucceededCount()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = new StressTestOptions(
            new Uri("https://example.com/api"),
            "payload.json",
            HttpMethod.Post,
            2,
            TimeSpan.FromSeconds(1),
            1);
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null),
            new RequestOutcome(1, 2, true, false, 200, TimeSpan.FromMilliseconds(60), null)
        };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionStart(options);
        reporter.WriteSessionComplete(report);

        var output = writer.ToString();
        Assert.Contains("https://example.com/api", output, StringComparison.Ordinal);
        Assert.Contains("POST", output, StringComparison.Ordinal);
        Assert.Contains("Succeeded: 2", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_FailuresPresent_ContainsFailedCount()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions();
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, false, false, 500, TimeSpan.FromMilliseconds(40), "error")
        };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionComplete(report);

        Assert.Contains("Failed:    1", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_CancelledSession_ContainsCancelledIndicator()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions();
        var report = new SessionReport(options, [], true);

        reporter.WriteSessionComplete(report);

        Assert.Contains("Cancelled", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_NoSuccessfulLatencies_PrintsNotApplicable()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions();
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, false, false, 500, TimeSpan.FromMilliseconds(40), "error")
        };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionComplete(report);

        Assert.Contains("Latency:   N/A", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_AuthConfigured_ShowsConfiguredIndicator()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Auth = "Bearer secret-token" };

        reporter.WriteSessionStart(options);

        var output = writer.ToString();
        Assert.Contains("Auth:     configured", output, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_AuthOmitted_DoesNotShowAuthLine()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);

        reporter.WriteSessionStart(CreateOptions());

        Assert.DoesNotContain("Auth:", writer.ToString(), StringComparison.Ordinal);
    }

    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, 1, TimeSpan.FromSeconds(1), 1);
}
