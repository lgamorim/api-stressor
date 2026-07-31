namespace Stressor.Core;

/// <summary>Merged stress-test configuration values before option parsing.</summary>
public sealed record StressTestConfigurationValues
{
    /// <summary>API endpoint URL.</summary>
    public string? Url { get; init; }

    /// <summary>Path to the JSON payload file.</summary>
    public string? Payload { get; init; }

    /// <summary>HTTP method.</summary>
    public string Method { get; init; } = "POST";

    /// <summary>Requests to send per cycle.</summary>
    public int? Requests { get; init; }

    /// <summary>Minimum delay between consecutive request starts.</summary>
    public string? Interval { get; init; }

    /// <summary>Number of cycles to execute.</summary>
    public int Cycles { get; init; } = 1;

    /// <summary>Authorization header value.</summary>
    public string? Auth { get; init; }

    /// <summary>Custom HTTP headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>HTTP status codes that count as success.</summary>
    public IReadOnlySet<int> ExpectStatus { get; init; } = new HashSet<int>();

    /// <summary>Per-request output mode.</summary>
    public string? Verbose { get; init; }

    /// <summary>Load handling mode.</summary>
    public string Load { get; init; } = "gentle-pacing";

    /// <summary>Max parallel requests per wave.</summary>
    public int Batch { get; init; } = 1;

    /// <summary>Per-request timeout.</summary>
    public string Timeout { get; init; } = "100s";

    /// <summary>Minimum wait after a cycle completes before the next cycle starts.</summary>
    public string CycleInterval { get; init; } = "0s";
}
