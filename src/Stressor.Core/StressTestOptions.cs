namespace Stressor.Core;

public sealed record StressTestOptions(
    Uri Url,
    string PayloadFilePath,
    HttpMethod Method,
    int RequestsPerInterval,
    TimeSpan Interval,
    int Cycles,
    string? Auth = null);
