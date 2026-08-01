namespace Stressor.Core;

/// <summary>JSON session report root document (schema v1).</summary>
public sealed record SessionReportDocument
{
    /// <summary>Report schema version.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Stressor application version.</summary>
    public required string StressorVersion { get; init; }

    /// <summary>UTC timestamp when the session completed.</summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>Whether the session ended due to cancellation.</summary>
    public required bool WasCancelled { get; init; }

    /// <summary>Process exit code for the session.</summary>
    public required int ExitCode { get; init; }

    /// <summary>Session configuration summary.</summary>
    public required SessionReportConfigurationDocument Configuration { get; init; }

    /// <summary>Aggregated session results.</summary>
    public required SessionReportSummaryDocument Summary { get; init; }

    /// <summary>Per-request outcomes in session order.</summary>
    public required IReadOnlyList<SessionReportOutcomeDocument> Outcomes { get; init; }
}

/// <summary>Redacted session configuration in a JSON report.</summary>
public sealed record SessionReportConfigurationDocument
{
    /// <summary>Target URL.</summary>
    public required string Url { get; init; }

    /// <summary>HTTP method.</summary>
    public required string Method { get; init; }

    /// <summary>Requests per cycle.</summary>
    public required int RequestsPerCycle { get; init; }

    /// <summary>Number of cycles.</summary>
    public int? Cycles { get; init; }

    /// <summary>Session duration in milliseconds, when duration-limited.</summary>
    public double? DurationMs { get; init; }

    /// <summary>Interval between request starts in milliseconds.</summary>
    public required double IntervalMs { get; init; }

    /// <summary>Load mode name.</summary>
    public required string Load { get; init; }

    /// <summary>Batch size.</summary>
    public required int Batch { get; init; }

    /// <summary>Per-request timeout in milliseconds.</summary>
    public required double TimeoutMs { get; init; }

    /// <summary>Cycle gap in milliseconds.</summary>
    public required double CycleIntervalMs { get; init; }

    /// <summary>Verbose mode name.</summary>
    public required string Verbose { get; init; }

    /// <summary>Whether authorization was configured.</summary>
    public required bool AuthConfigured { get; init; }

    /// <summary>Number of custom headers configured.</summary>
    public required int HeadersCount { get; init; }

    /// <summary>Expected HTTP status codes, when configured.</summary>
    public IReadOnlyList<int> ExpectedStatusCodes { get; init; } = [];
}

/// <summary>Aggregated session metrics in a JSON report.</summary>
public sealed record SessionReportSummaryDocument
{
    /// <summary>Total requests attempted.</summary>
    public required int TotalRequests { get; init; }

    /// <summary>Successful requests.</summary>
    public required int Succeeded { get; init; }

    /// <summary>Failed requests.</summary>
    public required int Failed { get; init; }

    /// <summary>Cancelled requests.</summary>
    public required int Cancelled { get; init; }

    /// <summary>Latency statistics in milliseconds, or null when none succeeded.</summary>
    public SessionReportLatencyDocument? LatencyMs { get; init; }
}

/// <summary>Latency percentiles in milliseconds.</summary>
public sealed record SessionReportLatencyDocument
{
    /// <summary>Minimum latency.</summary>
    public required double Min { get; init; }

    /// <summary>Average latency.</summary>
    public required double Avg { get; init; }

    /// <summary>Maximum latency.</summary>
    public required double Max { get; init; }

    /// <summary>50th percentile latency.</summary>
    public required double P50 { get; init; }

    /// <summary>95th percentile latency.</summary>
    public required double P95 { get; init; }

    /// <summary>99th percentile latency.</summary>
    public required double P99 { get; init; }
}

/// <summary>Single request outcome in a JSON report.</summary>
public sealed record SessionReportOutcomeDocument
{
    /// <summary>Cycle number.</summary>
    public required int Cycle { get; init; }

    /// <summary>Request number within the cycle.</summary>
    public required int Request { get; init; }

    /// <summary>1-based index across the session.</summary>
    public required int SessionIndex { get; init; }

    /// <summary>Payload variant index.</summary>
    public required int PayloadIndex { get; init; }

    /// <summary>Total payload variants.</summary>
    public required int PayloadCount { get; init; }

    /// <summary>Whether the request succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Whether the request was cancelled.</summary>
    public required bool Cancelled { get; init; }

    /// <summary>HTTP status code, when available.</summary>
    public int? StatusCode { get; init; }

    /// <summary>Request latency in milliseconds.</summary>
    public required double LatencyMs { get; init; }

    /// <summary>Error message, when the request failed.</summary>
    public string? ErrorMessage { get; init; }
}
