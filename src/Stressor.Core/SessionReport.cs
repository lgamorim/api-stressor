namespace Stressor.Core;

public sealed class SessionReport
{
    public SessionReport(
        StressTestOptions options,
        IReadOnlyList<RequestOutcome> outcomes,
        bool wasCancelled)
    {
        Options = options;
        Outcomes = outcomes;
        WasCancelled = wasCancelled;
    }

    public StressTestOptions Options { get; }

    public IReadOnlyList<RequestOutcome> Outcomes { get; }

    public bool WasCancelled { get; }

    public int TotalRequests => Outcomes.Count;

    public int SucceededCount => Outcomes.Count(o => o.IsSuccess);

    public int FailedCount => Outcomes.Count(o => !o.IsSuccess && !o.IsCancelled);

    public int CancelledCount => Outcomes.Count(o => o.IsCancelled);

    public TimeSpan? MinLatency
    {
        get
        {
            var latencies = GetSuccessfulLatencies().ToList();
            return latencies.Count == 0 ? null : latencies.Min();
        }
    }

    public TimeSpan? MaxLatency
    {
        get
        {
            var latencies = GetSuccessfulLatencies().ToList();
            return latencies.Count == 0 ? null : latencies.Max();
        }
    }

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

    private IEnumerable<TimeSpan> GetSuccessfulLatencies() =>
        Outcomes.Where(o => o.IsSuccess).Select(o => o.Latency);
}
