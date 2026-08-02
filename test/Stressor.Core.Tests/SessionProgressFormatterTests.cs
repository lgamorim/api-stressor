namespace Stressor.Core.Tests;

public class SessionProgressFormatterTests
{
    [Fact]
    public void Should_FormatCycleLimitedProgress_When_TotalRequestsKnown()
    {
        var snapshot = new SessionProgressSnapshot(
            CompletedRequests: 50,
            TotalRequests: 600,
            Succeeded: 48,
            Failed: 2,
            Cancelled: 0,
            CycleNumber: 5,
            TotalCycles: 60,
            Elapsed: null,
            TotalDuration: null);

        var line = SessionProgressFormatter.Format(snapshot);

        Assert.Equal("[50/600]  OK 48  Fail 2", line);
    }

    [Fact]
    public void Should_IncludeCancelledCount_When_CancelledGreaterThanZero()
    {
        var snapshot = new SessionProgressSnapshot(
            CompletedRequests: 10,
            TotalRequests: 100,
            Succeeded: 8,
            Failed: 1,
            Cancelled: 1,
            CycleNumber: 1,
            TotalCycles: 10,
            Elapsed: null,
            TotalDuration: null);

        var line = SessionProgressFormatter.Format(snapshot);

        Assert.Equal("[10/100]  OK 8  Fail 1  Cancel 1", line);
    }

    [Fact]
    public void Should_FormatDurationLimitedProgress_When_ElapsedAndTotalKnown()
    {
        var snapshot = new SessionProgressSnapshot(
            CompletedRequests: 150,
            TotalRequests: null,
            Succeeded: 150,
            Failed: 0,
            Cancelled: 0,
            CycleNumber: 15,
            TotalCycles: null,
            Elapsed: TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(30),
            TotalDuration: TimeSpan.FromMinutes(5));

        var line = SessionProgressFormatter.Format(snapshot);

        Assert.Equal("[2m30s/5m00s]  cycle 15  OK 150  Fail 0", line);
    }
}
