namespace Stressor.Core;

using System.Globalization;

public sealed class ConsoleSessionReporter : IConsoleSessionReporter
{
    private readonly TextWriter output;

    public ConsoleSessionReporter()
        : this(Console.Out)
    {
    }

    public ConsoleSessionReporter(TextWriter output)
    {
        this.output = output;
    }

    public void WriteSessionStart(StressTestOptions options)
    {
        var totalRequests = options.RequestsPerInterval * options.Cycles;
        output.WriteLine("Stress test starting");
        output.WriteLine($"  URL:      {options.Url.ToString()}");
        output.WriteLine($"  Method:   {options.Method.Method}");

        if (!string.IsNullOrWhiteSpace(options.Auth))
        {
            output.WriteLine("  Auth:     configured");
        }

        if (options.Load == LoadMode.Batch)
        {
            output.WriteLine($"  Rate:     {options.RequestsPerInterval.ToString(CultureInfo.InvariantCulture)} requests/cycle, batch {options.Batch.ToString(CultureInfo.InvariantCulture)}, {FormatInterval(options.Interval)} between wave starts");
        }
        else
        {
            output.WriteLine($"  Rate:     {options.RequestsPerInterval.ToString(CultureInfo.InvariantCulture)} requests/cycle, {FormatInterval(options.Interval)} between starts");
        }
        output.WriteLine($"  Load:     {FormatLoadMode(options.Load)}");
        output.WriteLine($"  Timeout:  {FormatInterval(options.RequestTimeout)}");
        if (options.CycleInterval > TimeSpan.Zero)
        {
            output.WriteLine($"  Cycle gap: {FormatInterval(options.CycleInterval)}");
        }

        if (options.Verbose != VerboseMode.Off)
        {
            output.WriteLine($"  Verbose:  {FormatVerboseMode(options.Verbose)}");
        }

        output.WriteLine($"  Cycles:   {options.Cycles.ToString(CultureInfo.InvariantCulture)} ({totalRequests.ToString(CultureInfo.InvariantCulture)} total requests)");
        output.WriteLine();
    }

    public void WriteCycleSummary(int cycleNumber, int totalCycles, IReadOnlyList<RequestOutcome> cycleOutcomes)
    {
        var succeeded = cycleOutcomes.Count(o => o.IsSuccess);
        var failed = cycleOutcomes.Count(o => !o.IsSuccess && !o.IsCancelled);
        var successfulLatencies = cycleOutcomes.Where(o => o.IsSuccess).Select(o => o.Latency).ToList();
        var averageMs = successfulLatencies.Count == 0
            ? 0
            : successfulLatencies.Average(l => l.TotalMilliseconds);

        output.WriteLine(
            $"Cycle {cycleNumber.ToString(CultureInfo.InvariantCulture)}/{totalCycles.ToString(CultureInfo.InvariantCulture)}  OK {succeeded.ToString(CultureInfo.InvariantCulture)}  Fail {failed.ToString(CultureInfo.InvariantCulture)}  Avg {averageMs.ToString("F0", CultureInfo.InvariantCulture)}ms");
    }

    public void WriteVerboseRequest(
        int cycleNumber,
        int totalCycles,
        int requestNumber,
        int requestsPerInterval,
        string? requestPayload,
        int sessionRequestIndex,
        int sessionTotalRequests,
        int payloadIndex,
        int payloadCount,
        RequestOutcome outcome)
    {
        output.WriteLine(
            $"({sessionRequestIndex.ToString(CultureInfo.InvariantCulture)}/{sessionTotalRequests.ToString(CultureInfo.InvariantCulture)}) Request {requestNumber.ToString(CultureInfo.InvariantCulture)}/{requestsPerInterval.ToString(CultureInfo.InvariantCulture)} (cycle {cycleNumber.ToString(CultureInfo.InvariantCulture)}/{totalCycles.ToString(CultureInfo.InvariantCulture)}) payload {payloadIndex.ToString(CultureInfo.InvariantCulture)}/{payloadCount.ToString(CultureInfo.InvariantCulture)}");

        if (requestPayload is not null)
        {
            output.WriteLine(requestPayload);
        }

        if (!string.IsNullOrEmpty(outcome.ResponseBody))
        {
            output.WriteLine(outcome.ResponseBody);
        }

        if (outcome.IsSuccess)
        {
            var latencyMs = outcome.Latency.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
            output.WriteLine($"{ConsoleStyling.FormatSuccessPrefix(output)}HTTP {outcome.StatusCode!.Value.ToString(CultureInfo.InvariantCulture)} {latencyMs}ms");
        }
        else if (outcome.ErrorMessage is not null)
        {
            output.WriteLine($"{ConsoleStyling.FormatErrorPrefix(output)}{outcome.ErrorMessage}");
        }

        output.WriteLine();
    }

    public void WriteSessionComplete(SessionReport report)
    {
        output.WriteLine();
        output.WriteLine("Session complete");

        if (report.WasCancelled)
        {
            output.WriteLine("  Status:   Cancelled");
        }

        output.WriteLine($"  Succeeded: {report.SucceededCount.ToString(CultureInfo.InvariantCulture)}");
        output.WriteLine($"  Failed:    {report.FailedCount.ToString(CultureInfo.InvariantCulture)}");

        if (report.CancelledCount > 0)
        {
            output.WriteLine($"  Cancelled: {report.CancelledCount.ToString(CultureInfo.InvariantCulture)}");
        }

        if (report.MinLatency is null)
        {
            output.WriteLine("  Latency:   N/A");
        }
        else
        {
            output.WriteLine(
                $"  Latency:   min {report.MinLatency.Value.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}ms  avg {report.AverageLatency!.Value.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}ms  max {report.MaxLatency!.Value.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}ms");
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

        var sessionTotalRequests = report.Options.RequestsPerInterval * report.Options.Cycles;
        output.WriteLine();
        output.WriteLine($"Failures ({failures.Count.ToString(CultureInfo.InvariantCulture)}):");
        foreach (var outcome in failures)
        {
            var sessionIndex = (outcome.CycleNumber - 1) * report.Options.RequestsPerInterval + outcome.RequestNumber;
            var summary = FormatDigestSummary(outcome);
            var latencyPart = outcome.StatusCode is not null || !outcome.IsCancelled
                ? $" {outcome.Latency.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}ms"
                : string.Empty;
            output.WriteLine(
                $"  ({sessionIndex.ToString(CultureInfo.InvariantCulture)}/{sessionTotalRequests.ToString(CultureInfo.InvariantCulture)}) {summary} payload {outcome.PayloadIndex.ToString(CultureInfo.InvariantCulture)}/{outcome.PayloadCount.ToString(CultureInfo.InvariantCulture)}{latencyPart}");
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
