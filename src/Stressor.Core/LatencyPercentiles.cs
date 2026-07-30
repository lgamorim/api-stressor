namespace Stressor.Core;

/// <summary>Computes latency percentiles using the nearest-rank method (NIST).</summary>
public static class LatencyPercentiles
{
    /// <summary>
    /// Returns the latency at the given percentile using nearest-rank:
    /// rank = ceil(percentile / 100 × count), value at sorted position rank (1-based).
    /// For even counts, p50 takes the lower middle value rather than interpolating.
    /// </summary>
    /// <param name="latencies">Latencies to rank; need not be pre-sorted.</param>
    /// <param name="percentile">Percentile in the range 0–100.</param>
    /// <returns>The latency at the percentile, or <see langword="null"/> when empty.</returns>
    public static TimeSpan? GetPercentile(IReadOnlyList<TimeSpan> latencies, int percentile)
    {
        if (latencies.Count == 0)
        {
            return null;
        }

        var sorted = latencies.OrderBy(l => l.Ticks).ToList();
        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Count);
        return sorted[rank - 1];
    }
}
