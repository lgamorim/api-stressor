namespace Stressor.Core;

/// <summary>Deserialized contents of a JSON stress-test scenario file.</summary>
public sealed record StressTestScenarioDocument
{
    /// <summary>API endpoint URL.</summary>
    public string? Url { get; init; }

    /// <summary>Path to the JSON payload file.</summary>
    public string? Payload { get; init; }

    /// <summary>HTTP method.</summary>
    public string? Method { get; init; }

    /// <summary>Requests to send per cycle.</summary>
    public int? Requests { get; init; }

    /// <summary>Minimum delay between consecutive request starts.</summary>
    public string? Interval { get; init; }

    /// <summary>Number of cycles to execute.</summary>
    public int? Cycles { get; init; }

    /// <summary>Authorization header value.</summary>
    public string? Auth { get; init; }

    /// <summary>Per-request output mode: failures or full.</summary>
    public string? Verbose { get; init; }

    /// <summary>Load handling mode.</summary>
    public string? Load { get; init; }

    /// <summary>Max parallel requests per wave.</summary>
    public int? Batch { get; init; }

    /// <summary>Per-request timeout.</summary>
    public string? Timeout { get; init; }

    /// <summary>Minimum wait after a cycle completes before the next cycle starts.</summary>
    public string? CycleInterval { get; init; }
}
