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

        output.WriteLine($"  Rate:     {options.RequestsPerInterval.ToString(CultureInfo.InvariantCulture)} requests/cycle, {FormatInterval(options.Interval)} between starts");
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
        string payload,
        bool prettyPrint,
        RequestOutcome outcome)
    {
        output.WriteLine(
            $"Request {requestNumber.ToString(CultureInfo.InvariantCulture)}/{requestsPerInterval.ToString(CultureInfo.InvariantCulture)} (cycle {cycleNumber.ToString(CultureInfo.InvariantCulture)}/{totalCycles.ToString(CultureInfo.InvariantCulture)})");
        output.WriteLine(prettyPrint ? JsonPayloadFormatter.PrettyPrint(payload) : payload);

        if (outcome.IsSuccess)
        {
            var latencyMs = outcome.Latency.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
            output.WriteLine($"{ConsoleStyling.FormatSuccessPrefix(output)}{latencyMs}ms");
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
    }

    private static string FormatInterval(TimeSpan interval)
    {
        if (interval.TotalSeconds >= 1 && interval.TotalMilliseconds % 1000 == 0)
        {
            return $"{interval.TotalSeconds:F0}s";
        }

        return $"{interval.TotalMilliseconds:F0}ms";
    }
}
