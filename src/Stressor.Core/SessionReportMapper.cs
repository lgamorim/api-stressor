namespace Stressor.Core;

/// <summary>Maps domain session reports to JSON report documents.</summary>
public static class SessionReportMapper
{
    /// <summary>Builds a JSON report document from a completed session.</summary>
    public static SessionReportDocument ToDocument(
        SessionReport report,
        int exitCode,
        string stressorVersion,
        DateTimeOffset completedAt)
    {
        var options = report.Options;

        return new SessionReportDocument
        {
            StressorVersion = stressorVersion,
            CompletedAt = completedAt,
            WasCancelled = report.WasCancelled,
            ExitCode = exitCode,
            Configuration = new SessionReportConfigurationDocument
            {
                Url = options.Url.ToString(),
                Method = options.Method.Method,
                RequestsPerCycle = options.RequestsPerInterval,
                Cycles = options.IsDurationLimited ? null : options.Cycles,
                DurationMs = options.IsDurationLimited ? ToMilliseconds(options.Duration!.Value) : null,
                IntervalMs = ToMilliseconds(options.Interval),
                Load = FormatLoadMode(options.Load),
                Batch = options.Batch,
                TimeoutMs = ToMilliseconds(options.RequestTimeout),
                CycleIntervalMs = ToMilliseconds(options.CycleInterval),
                Verbose = FormatVerboseMode(options.Verbose),
                Progress = options.Progress,
                AuthConfigured = !string.IsNullOrWhiteSpace(options.Auth),
                HeadersCount = options.Headers.Count,
                ExpectedStatusCodes = options.ExpectedStatusCodes.OrderBy(code => code).ToList()
            },
            Summary = new SessionReportSummaryDocument
            {
                TotalRequests = report.TotalRequests,
                Succeeded = report.SucceededCount,
                Failed = report.FailedCount,
                Cancelled = report.CancelledCount,
                LatencyMs = MapLatency(report)
            },
            Outcomes = MapOutcomes(report.Outcomes)
        };
    }

    private static SessionReportLatencyDocument? MapLatency(SessionReport report)
    {
        if (report.MinLatency is not { } minLatency
            || report.AverageLatency is not { } averageLatency
            || report.MaxLatency is not { } maxLatency
            || report.P50Latency is not { } p50Latency
            || report.P95Latency is not { } p95Latency
            || report.P99Latency is not { } p99Latency)
        {
            return null;
        }

        return new SessionReportLatencyDocument
        {
            Min = ToMilliseconds(minLatency),
            Avg = ToMilliseconds(averageLatency),
            Max = ToMilliseconds(maxLatency),
            P50 = ToMilliseconds(p50Latency),
            P95 = ToMilliseconds(p95Latency),
            P99 = ToMilliseconds(p99Latency)
        };
    }

    private static IReadOnlyList<SessionReportOutcomeDocument> MapOutcomes(IReadOnlyList<RequestOutcome> outcomes)
    {
        var documents = new List<SessionReportOutcomeDocument>(outcomes.Count);

        for (var index = 0; index < outcomes.Count; index++)
        {
            var outcome = outcomes[index];
            documents.Add(new SessionReportOutcomeDocument
            {
                Cycle = outcome.CycleNumber,
                Request = outcome.RequestNumber,
                SessionIndex = index + 1,
                PayloadIndex = outcome.PayloadIndex,
                PayloadCount = outcome.PayloadCount,
                Success = outcome.IsSuccess,
                Cancelled = outcome.IsCancelled,
                StatusCode = outcome.StatusCode,
                LatencyMs = ToMilliseconds(outcome.Latency),
                ErrorMessage = outcome.ErrorMessage
            });
        }

        return documents;
    }

    private static double ToMilliseconds(TimeSpan duration) => duration.TotalMilliseconds;

    private static string FormatLoadMode(LoadMode loadMode) => loadMode switch
    {
        LoadMode.GentlePacing => "gentle-pacing",
        LoadMode.FixedRate => "fixed-rate",
        LoadMode.Batch => "batch",
        _ => loadMode.ToString()
    };

    private static string FormatVerboseMode(VerboseMode verboseMode) => verboseMode switch
    {
        VerboseMode.Off => "off",
        VerboseMode.Failures => "failures",
        VerboseMode.Full => "full",
        _ => verboseMode.ToString()
    };
}
