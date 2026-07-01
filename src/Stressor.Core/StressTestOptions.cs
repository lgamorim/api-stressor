namespace Stressor.Core;

public sealed record StressTestOptions(
    Uri Url,
    string PayloadFilePath,
    HttpMethod Method,
    int RequestsPerInterval,
    TimeSpan Interval,
    int Cycles,
    string? Auth = null,
    bool Verbose = false,
    bool PrettyPrint = false,
    LoadMode Load = LoadMode.GentlePacing);
