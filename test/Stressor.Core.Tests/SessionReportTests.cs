namespace Stressor.Core.Tests;

public class SessionReportTests
{
    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, 1, TimeSpan.FromSeconds(1), 1);

    [Fact]
    public void EmptyOutcomeList_HasZeroCounts()
    {
        var report = new SessionReport(CreateOptions(), [], false);

        Assert.Equal(0, report.TotalRequests);
        Assert.Equal(0, report.SucceededCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal(0, report.CancelledCount);
        Assert.Null(report.MinLatency);
        Assert.Null(report.AverageLatency);
        Assert.Null(report.MaxLatency);
    }

    [Fact]
    public void AllSuccesses_ComputesLatencyStats()
    {
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(30), null),
            new RequestOutcome(1, 2, true, false, 200, TimeSpan.FromMilliseconds(50), null),
            new RequestOutcome(1, 3, true, false, 200, TimeSpan.FromMilliseconds(70), null)
        };

        var report = new SessionReport(CreateOptions(), outcomes, false);

        Assert.Equal(3, report.SucceededCount);
        Assert.Equal(TimeSpan.FromMilliseconds(30), report.MinLatency);
        Assert.Equal(TimeSpan.FromMilliseconds(70), report.MaxLatency);
        Assert.Equal(TimeSpan.FromMilliseconds(50), report.AverageLatency);
    }

    [Fact]
    public void SingleSuccess_MinEqualsAvgEqualsMax()
    {
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(42), null)
        };

        var report = new SessionReport(CreateOptions(), outcomes, false);

        Assert.Equal(report.MinLatency, report.AverageLatency);
        Assert.Equal(report.AverageLatency, report.MaxLatency);
    }

    [Fact]
    public void NoSuccesses_LatencyStatsAreNull()
    {
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, false, false, 500, TimeSpan.FromMilliseconds(42), "error")
        };

        var report = new SessionReport(CreateOptions(), outcomes, false);

        Assert.Null(report.MinLatency);
        Assert.Null(report.AverageLatency);
        Assert.Null(report.MaxLatency);
    }
}
