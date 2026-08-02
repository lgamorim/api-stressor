namespace Stressor.Core;

/// <summary>Session-wide progress state at a cycle boundary.</summary>
public sealed record SessionProgressSnapshot(
    int CompletedRequests,
    int? TotalRequests,
    int Succeeded,
    int Failed,
    int Cancelled,
    int CycleNumber,
    int? TotalCycles,
    TimeSpan? Elapsed,
    TimeSpan? TotalDuration);
