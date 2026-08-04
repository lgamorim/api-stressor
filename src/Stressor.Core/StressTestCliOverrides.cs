namespace Stressor.Core;

/// <summary>CLI values and which options were explicitly specified on the command line.</summary>
public sealed record StressTestCliOverrides
{
    /// <summary>Names of options explicitly provided on the command line.</summary>
    public IReadOnlySet<string> SpecifiedOptions { get; init; } = new HashSet<string>();

    /// <summary>API endpoint URL from the CLI.</summary>
    public string? Url { get; init; }

    /// <summary>Path to the JSON payload file from the CLI.</summary>
    public string? Payload { get; init; }

    /// <summary>HTTP method from the CLI.</summary>
    public string? Method { get; init; }

    /// <summary>Requests per cycle from the CLI.</summary>
    public int? Requests { get; init; }

    /// <summary>Interval from the CLI.</summary>
    public string? Interval { get; init; }

    /// <summary>Cycles from the CLI.</summary>
    public int? Cycles { get; init; }

    /// <summary>Authorization header value from the CLI.</summary>
    public string? Auth { get; init; }

    /// <summary>Path to a JSON headers file from the CLI.</summary>
    public string? HeadersFile { get; init; }

    /// <summary>Header strings from repeatable --header flags.</summary>
    public IReadOnlyList<string> Header { get; init; } = [];

    /// <summary>Expected status code strings from repeatable --expect-status flags.</summary>
    public IReadOnlyList<string> ExpectStatus { get; init; } = [];

    /// <summary>Verbose mode from the CLI.</summary>
    public string? Verbose { get; init; }

    /// <summary>Load mode from the CLI.</summary>
    public string? Load { get; init; }

    /// <summary>Batch size from the CLI.</summary>
    public int? Batch { get; init; }

    /// <summary>Timeout from the CLI.</summary>
    public string? Timeout { get; init; }

    /// <summary>Cycle interval from the CLI.</summary>
    public string? CycleInterval { get; init; }

    /// <summary>Report file path from the CLI.</summary>
    public string? Report { get; init; }

    /// <summary>Session duration from the CLI.</summary>
    public string? Duration { get; init; }

    /// <summary>Whether progress output is enabled from the CLI.</summary>
    public bool Progress { get; init; }

    /// <summary>Whether dry-run mode is enabled from the CLI.</summary>
    public bool DryRun { get; init; }

    /// <summary>Returns whether the named option was explicitly specified on the command line.</summary>
    public bool IsSpecified(string optionName) => SpecifiedOptions.Contains(optionName);
}
