namespace Stressor.Core;

/// <summary>Aggregated results for a completed stress-test session.</summary>
public sealed class SessionReport
{
    /// <summary>Creates a report from the session options, outcomes, and cancellation state.</summary>
    public SessionReport(
        StressTestOptions options,
        IReadOnlyList<RequestOutcome> outcomes,
        bool wasCancelled)
    {
        Options = options;
        Outcomes = outcomes;
        WasCancelled = wasCancelled;
    }

    /// <summary>Options used for the session.</summary>
    public StressTestOptions Options { get; }

    /// <summary>Outcome of every request attempted in the session.</summary>
    public IReadOnlyList<RequestOutcome> Outcomes { get; }

    /// <summary>Whether the session ended due to cancellation.</summary>
    public bool WasCancelled { get; }

    /// <summary>Total number of requests attempted.</summary>
    public int TotalRequests => Outcomes.Count;

    /// <summary>Number of successful requests.</summary>
    public int SucceededCount => Outcomes.Count(o => o.IsSuccess);

    /// <summary>Number of failed requests (excluding cancellations).</summary>
    public int FailedCount => Outcomes.Count(o => !o.IsSuccess && !o.IsCancelled);

    /// <summary>Number of cancelled requests.</summary>
    public int CancelledCount => Outcomes.Count(o => o.IsCancelled);

    /// <summary>Minimum latency among successful requests, or <see langword="null"/> when none succeeded.</summary>
    public TimeSpan? MinLatency
    {
        get
        {
            var latencies = GetSuccessfulLatencies().ToList();
            return latencies.Count == 0 ? null : latencies.Min();
        }
    }

    /// <summary>Maximum latency among successful requests, or <see langword="null"/> when none succeeded.</summary>
    public TimeSpan? MaxLatency
    {
        get
        {
            var latencies = GetSuccessfulLatencies().ToList();
            return latencies.Count == 0 ? null : latencies.Max();
        }
    }

    /// <summary>Average latency among successful requests, or <see langword="null"/> when none succeeded.</summary>
    public TimeSpan? AverageLatency
    {
        get
        {
            var latencies = GetSuccessfulLatencies().ToList();
            if (latencies.Count == 0)
            {
                return null;
            }

            var averageTicks = latencies.Average(l => l.Ticks);
            return TimeSpan.FromTicks((long)averageTicks);
        }
    }

    /// <summary>50th-percentile latency among successful requests, or <see langword="null"/> when none succeeded.</summary>
    public TimeSpan? P50Latency => GetPercentile(50);

    /// <summary>95th-percentile latency among successful requests, or <see langword="null"/> when none succeeded.</summary>
    public TimeSpan? P95Latency => GetPercentile(95);

    /// <summary>99th-percentile latency among successful requests, or <see langword="null"/> when none succeeded.</summary>
    public TimeSpan? P99Latency => GetPercentile(99);

    private TimeSpan? GetPercentile(int percentile)
    {
        var latencies = GetSuccessfulLatencies().ToList();
        return LatencyPercentiles.GetPercentile(latencies, percentile);
    }

    private IEnumerable<TimeSpan> GetSuccessfulLatencies() =>
        Outcomes.Where(o => o.IsSuccess).Select(o => o.Latency);
}
