namespace Stressor.Core;

/// <summary>Result of a single HTTP request within a stress session.</summary>
public sealed record RequestOutcome(
    int CycleNumber,
    int RequestNumber,
    bool IsSuccess,
    bool IsCancelled,
    int? StatusCode,
    TimeSpan Latency,
    string? ErrorMessage,
    string? ResponseBody = null,
    int PayloadIndex = 1,
    int PayloadCount = 1);
