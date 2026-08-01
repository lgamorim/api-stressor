namespace Stressor.Core.Tests;

public class SessionReportMapperTests
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_MapSummaryCounts_When_SessionCompleted()
    {
        var options = CreateOptions();
        var outcomes = new List<RequestOutcome>
        {
            new(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null),
            new(1, 2, false, false, 500, TimeSpan.FromMilliseconds(60), "error")
        };
        var report = new SessionReport(options, outcomes, false);

        var document = SessionReportMapper.ToDocument(report, 1, "0.12.0-alpha", CompletedAt);

        Assert.Equal(2, document.Summary.TotalRequests);
        Assert.Equal(1, document.Summary.Succeeded);
        Assert.Equal(1, document.Summary.Failed);
        Assert.Equal(0, document.Summary.Cancelled);
        Assert.Equal(1, document.ExitCode);
    }

    [Fact]
    public void Should_MapLatencyStats_When_SuccessfulRequestsExist()
    {
        var options = CreateOptions();
        var outcomes = new List<RequestOutcome>
        {
            new(1, 1, true, false, 200, TimeSpan.FromMilliseconds(30), null),
            new(1, 2, true, false, 200, TimeSpan.FromMilliseconds(50), null)
        };
        var report = new SessionReport(options, outcomes, false);

        var document = SessionReportMapper.ToDocument(report, 0, "0.12.0-alpha", CompletedAt);

        Assert.NotNull(document.Summary.LatencyMs);
        Assert.Equal(30, document.Summary.LatencyMs.Min);
        Assert.Equal(40, document.Summary.LatencyMs.Avg);
        Assert.Equal(50, document.Summary.LatencyMs.Max);
    }

    [Fact]
    public void Should_ReturnNullLatency_When_NoSuccessfulRequests()
    {
        var options = CreateOptions();
        var outcomes = new List<RequestOutcome>
        {
            new(1, 1, false, false, 500, TimeSpan.FromMilliseconds(40), "error")
        };
        var report = new SessionReport(options, outcomes, false);

        var document = SessionReportMapper.ToDocument(report, 1, "0.12.0-alpha", CompletedAt);

        Assert.Null(document.Summary.LatencyMs);
    }

    [Fact]
    public void Should_RedactSecrets_When_MappingConfiguration()
    {
        var options = CreateOptions() with
        {
            Auth = "Bearer secret",
            Headers = new Dictionary<string, string> { ["X-Api-Key"] = "abc123" },
            ExpectedStatusCodes = new HashSet<int> { 201, 200 }
        };
        var report = new SessionReport(options, [], false);

        var document = SessionReportMapper.ToDocument(report, 0, "0.12.0-alpha", CompletedAt);

        Assert.True(document.Configuration.AuthConfigured);
        Assert.Equal(1, document.Configuration.HeadersCount);
        Assert.Equal([200, 201], document.Configuration.ExpectedStatusCodes);
        Assert.DoesNotContain("secret", document.Configuration.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_AssignSessionIndex_When_MappingOutcomes()
    {
        var options = CreateOptions();
        var outcomes = new List<RequestOutcome>
        {
            new(1, 1, true, false, 200, TimeSpan.FromMilliseconds(10), null),
            new(1, 2, true, false, 200, TimeSpan.FromMilliseconds(20), null)
        };
        var report = new SessionReport(options, outcomes, false);

        var document = SessionReportMapper.ToDocument(report, 0, "0.12.0-alpha", CompletedAt);

        Assert.Equal(1, document.Outcomes[0].SessionIndex);
        Assert.Equal(2, document.Outcomes[1].SessionIndex);
    }

    [Fact]
    public void Should_MapDurationMs_When_DurationLimited()
    {
        var options = CreateOptions() with { Duration = TimeSpan.FromMinutes(5) };
        var report = new SessionReport(options, [], false);

        var document = SessionReportMapper.ToDocument(report, 0, "0.12.0-alpha", CompletedAt);

        Assert.Null(document.Configuration.Cycles);
        Assert.Equal(300_000, document.Configuration.DurationMs);
    }

    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com/api"), "payload.json", HttpMethod.Post, 2, TimeSpan.FromSeconds(1), 1);
}
