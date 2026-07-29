namespace Stressor.Core;

/// <summary>Controls how request timing and concurrency are applied during a stress session.</summary>
public enum LoadMode
{
    /// <summary>Waits for each response before starting the next request on the minimum interval.</summary>
    GentlePacing,

    /// <summary>Starts requests on a fixed schedule regardless of response latency.</summary>
    FixedRate,

    /// <summary>Sends parallel waves of up to <c>Batch</c> requests per interval.</summary>
    Batch,
}
