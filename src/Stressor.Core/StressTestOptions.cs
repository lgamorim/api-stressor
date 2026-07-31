namespace Stressor.Core;

/// <summary>Configuration for a single stress-test session.</summary>
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
    /// <summary>Default per-request timeout when none is specified.</summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(100);

    /// <summary>Custom HTTP headers sent with each request.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>HTTP status codes that count as success. Empty means default 2xx.</summary>
    public IReadOnlySet<int> ExpectedStatusCodes { get; init; } = new HashSet<int>();

    /// <summary>Maximum time to wait for a single HTTP response.</summary>
    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    /// <summary>Minimum wait after a cycle completes before the next cycle starts.</summary>
    public TimeSpan CycleInterval { get; init; } = TimeSpan.Zero;
}
