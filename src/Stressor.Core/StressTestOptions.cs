namespace Stressor.Core;

public sealed record StressTestOptions(
    Uri Url,
    string PayloadFilePath,
    HttpMethod Method,
    int RequestsPerInterval,
    TimeSpan Interval,
    int Cycles,
    string? Auth = null,
    VerboseMode Verbose = VerboseMode.Off,
    LoadMode Load = LoadMode.GentlePacing,
    int Batch = 1)
{
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(100);

    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    public TimeSpan CycleInterval { get; init; } = TimeSpan.Zero;
}
