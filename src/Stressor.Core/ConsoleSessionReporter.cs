namespace Stressor.Core;

using System.Globalization;

/// <summary>Writes formatted stress-session output to a <see cref="TextWriter"/>.</summary>
public sealed class ConsoleSessionReporter : IConsoleSessionReporter
{
    private readonly TextWriter _output;

    /// <summary>Writes session output to standard output.</summary>
    public ConsoleSessionReporter()
        : this(Console.Out)
    {
    }

    /// <summary>Writes session output to the given writer.</summary>
    public ConsoleSessionReporter(TextWriter output)
    {
        _output = output;
    }

    /// <inheritdoc />
    public void WriteSessionStart(StressTestOptions options)
    {
        _output.WriteLine("Stress test starting");
        _output.WriteLine($"  URL:      {options.Url.ToString()}");
        _output.WriteLine($"  Method:   {options.Method.Method}");

        if (!string.IsNullOrWhiteSpace(options.Auth))
        {
            _output.WriteLine("  Auth:     configured");
        }

        if (options.Headers.Count > 0)
        {
            _output.WriteLine($"  Headers:  {options.Headers.Count.ToString(CultureInfo.InvariantCulture)} configured");
        }

        if (options.ExpectedStatusCodes.Count > 0)
        {
            var codes = string.Join(
                ", ",
                options.ExpectedStatusCodes.OrderBy(code => code).Select(code => code.ToString(CultureInfo.InvariantCulture)));
            _output.WriteLine($"  Expected: {codes}");
        }

        if (options.Load == LoadMode.Batch)
        {
            _output.WriteLine($"  Rate:     {options.RequestsPerInterval.ToString(CultureInfo.InvariantCulture)} requests/cycle, batch {options.Batch.ToString(CultureInfo.InvariantCulture)}, {FormatInterval(options.Interval)} between wave starts");
        }
        else
        {
            _output.WriteLine($"  Rate:     {options.RequestsPerInterval.ToString(CultureInfo.InvariantCulture)} requests/cycle, {FormatInterval(options.Interval)} between starts");
        }
        _output.WriteLine($"  Load:     {FormatLoadMode(options.Load)}");
        _output.WriteLine($"  Timeout:  {FormatInterval(options.RequestTimeout)}");
        if (options.CycleInterval > TimeSpan.Zero)
        {
            _output.WriteLine($"  Cycle gap: {FormatInterval(options.CycleInterval)}");
        }

        if (options.Verbose != VerboseMode.Off)
        {
            _output.WriteLine($"  Verbose:  {FormatVerboseMode(options.Verbose)}");
        }

        if (options.IsDurationLimited)
        {
            _output.WriteLine($"  Duration: {FormatInterval(options.Duration!.Value)} (runs until time elapsed)");
        }
        else
        {
            var totalRequests = options.RequestsPerInterval * options.Cycles;
            _output.WriteLine($"  Cycles:   {options.Cycles.ToString(CultureInfo.InvariantCulture)} ({totalRequests.ToString(CultureInfo.InvariantCulture)} total requests)");
        }

        _output.WriteLine();
    }

    /// <inheritdoc />
    public void WriteCycleSummary(int cycleNumber, int? totalCycles, IReadOnlyList<RequestOutcome> cycleOutcomes)
    {
        var succeeded = cycleOutcomes.Count(o => o.IsSuccess);
        var failed = cycleOutcomes.Count(o => !o.IsSuccess && !o.IsCancelled);
        var successfulLatencies = cycleOutcomes.Where(o => o.IsSuccess).Select(o => o.Latency).ToList();
        var averageMs = successfulLatencies.Count == 0
            ? 0
            : successfulLatencies.Average(l => l.TotalMilliseconds);

        var cycleLabel = totalCycles is null
            ? $"Cycle {cycleNumber.ToString(CultureInfo.InvariantCulture)}"
            : $"Cycle {cycleNumber.ToString(CultureInfo.InvariantCulture)}/{totalCycles.Value.ToString(CultureInfo.InvariantCulture)}";

        _output.WriteLine(
            $"{cycleLabel}  OK {succeeded.ToString(CultureInfo.InvariantCulture)}  Fail {failed.ToString(CultureInfo.InvariantCulture)}  Avg {averageMs.ToString("F0", CultureInfo.InvariantCulture)}ms");
    }

    /// <inheritdoc />
    public void WriteVerboseRequest(
        int cycleNumber,
        int? totalCycles,
        int requestNumber,
        int requestsPerInterval,
        string? requestPayload,
        int sessionRequestIndex,
        int? sessionTotalRequests,
        int payloadIndex,
        int payloadCount,
        RequestOutcome outcome)
    {
        var cycleLabel = totalCycles is null
            ? $"cycle {cycleNumber.ToString(CultureInfo.InvariantCulture)}"
            : $"cycle {cycleNumber.ToString(CultureInfo.InvariantCulture)}/{totalCycles.Value.ToString(CultureInfo.InvariantCulture)}";
        var sessionLabel = sessionTotalRequests is null
            ? $"({sessionRequestIndex.ToString(CultureInfo.InvariantCulture)})"
            : $"({sessionRequestIndex.ToString(CultureInfo.InvariantCulture)}/{sessionTotalRequests.Value.ToString(CultureInfo.InvariantCulture)})";

        _output.WriteLine(
            $"{sessionLabel} Request {requestNumber.ToString(CultureInfo.InvariantCulture)}/{requestsPerInterval.ToString(CultureInfo.InvariantCulture)} ({cycleLabel}) payload {payloadIndex.ToString(CultureInfo.InvariantCulture)}/{payloadCount.ToString(CultureInfo.InvariantCulture)}");

        if (requestPayload is not null)
        {
            _output.WriteLine(requestPayload);
        }

        if (!string.IsNullOrEmpty(outcome.ResponseBody))
        {
            _output.WriteLine(outcome.ResponseBody);
        }

        if (outcome is { IsSuccess: true, StatusCode: int statusCode })
        {
            var latencyMs = outcome.Latency.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
            _output.WriteLine($"{ConsoleStyling.FormatSuccessPrefix(_output)}HTTP {statusCode.ToString(CultureInfo.InvariantCulture)} {latencyMs}ms");
        }
        else if (outcome.ErrorMessage is not null)
        {
            _output.WriteLine($"{ConsoleStyling.FormatErrorPrefix(_output)}{outcome.ErrorMessage}");
        }

        _output.WriteLine();
    }

    /// <inheritdoc />
    public void WriteSessionComplete(SessionReport report)
    {
        _output.WriteLine();
        _output.WriteLine("Session complete");

        if (report.WasCancelled)
        {
            _output.WriteLine("  Status:   Cancelled");
        }

        _output.WriteLine($"  Succeeded: {report.SucceededCount.ToString(CultureInfo.InvariantCulture)}");
        _output.WriteLine($"  Failed:    {report.FailedCount.ToString(CultureInfo.InvariantCulture)}");

        if (report.CancelledCount > 0)
        {
            _output.WriteLine($"  Cancelled: {report.CancelledCount.ToString(CultureInfo.InvariantCulture)}");
        }

        if (report.MinLatency is { } minLatency
            && report.AverageLatency is { } averageLatency
            && report.MaxLatency is { } maxLatency
            && report.P50Latency is { } p50Latency
            && report.P95Latency is { } p95Latency
            && report.P99Latency is { } p99Latency)
        {
            _output.WriteLine(
                $"  Latency:   min {FormatLatencyMs(minLatency)}  avg {FormatLatencyMs(averageLatency)}  max {FormatLatencyMs(maxLatency)}  p50 {FormatLatencyMs(p50Latency)}  p95 {FormatLatencyMs(p95Latency)}  p99 {FormatLatencyMs(p99Latency)}");
        }
        else
        {
            _output.WriteLine("  Latency:   N/A");
        }

        if (!string.IsNullOrWhiteSpace(report.Options.ReportFilePath))
        {
            _output.WriteLine($"  Report:   {report.Options.ReportFilePath}");
        }

        WriteFailureDigest(report);
    }

    private void WriteFailureDigest(SessionReport report)
    {
        if (report.Options.Verbose == VerboseMode.Off)
        {
            return;
        }

        var failures = report.Outcomes.Where(o => !o.IsSuccess).ToList();
        if (failures.Count == 0)
        {
            return;
        }

        var sessionTotalRequests = report.Options.IsDurationLimited
            ? report.TotalRequests
            : report.Options.RequestsPerInterval * report.Options.Cycles;
        _output.WriteLine();
        _output.WriteLine($"Failures ({failures.Count.ToString(CultureInfo.InvariantCulture)}):");
        foreach (var outcome in failures)
        {
            var sessionIndex = (outcome.CycleNumber - 1) * report.Options.RequestsPerInterval + outcome.RequestNumber;
            var summary = FormatDigestSummary(outcome);
            var latencyPart = outcome.StatusCode is not null || !outcome.IsCancelled
                ? $" {outcome.Latency.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}ms"
                : string.Empty;
            var sessionLabel = report.Options.IsDurationLimited
                ? $"({sessionIndex.ToString(CultureInfo.InvariantCulture)})"
                : $"({sessionIndex.ToString(CultureInfo.InvariantCulture)}/{sessionTotalRequests.ToString(CultureInfo.InvariantCulture)})";
            _output.WriteLine(
                $"  {sessionLabel} {summary} payload {outcome.PayloadIndex.ToString(CultureInfo.InvariantCulture)}/{outcome.PayloadCount.ToString(CultureInfo.InvariantCulture)}{latencyPart}");
        }
    }

    private static string FormatDigestSummary(RequestOutcome outcome)
    {
        if (outcome.StatusCode is int statusCode)
        {
            return $"HTTP {statusCode.ToString(CultureInfo.InvariantCulture)}";
        }

        if (outcome.IsCancelled)
        {
            return "cancelled";
        }

        if (outcome.ErrorMessage is not null
            && outcome.ErrorMessage.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return "timeout";
        }

        return "error";
    }

    private static string FormatLatencyMs(TimeSpan latency) =>
        $"{latency.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}ms";

    private static string FormatVerboseMode(VerboseMode mode) =>
        mode switch
        {
            VerboseMode.Failures => "failures",
            VerboseMode.Full => "full",
            _ => mode.ToString()
        };

    private static string FormatLoadMode(LoadMode loadMode) =>
        loadMode switch
        {
            LoadMode.GentlePacing => "gentle-pacing",
            LoadMode.FixedRate => "fixed-rate",
            LoadMode.Batch => "batch",
            _ => loadMode.ToString()
        };

    private static string FormatInterval(TimeSpan interval)
    {
        if (interval.TotalSeconds >= 1 && interval.TotalMilliseconds % 1000 == 0)
        {
            return $"{interval.TotalSeconds:F0}s";
        }

        return $"{interval.TotalMilliseconds:F0}ms";
    }
}
