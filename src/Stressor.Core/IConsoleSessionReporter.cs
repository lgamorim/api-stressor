namespace Stressor.Core;

/// <summary>Writes stress-session progress and results to the console.</summary>
public interface IConsoleSessionReporter
{
    /// <summary>Writes the session header and configuration summary.</summary>
    void WriteSessionStart(StressTestOptions options);

    /// <summary>Writes per-cycle success, failure, and latency summary.</summary>
    void WriteCycleSummary(int cycleNumber, int totalCycles, IReadOnlyList<RequestOutcome> cycleOutcomes);

    /// <summary>Writes the final session summary and optional failure digest.</summary>
    void WriteSessionComplete(SessionReport report);

    /// <summary>Writes per-request detail when verbose mode is enabled.</summary>
    void WriteVerboseRequest(
        int cycleNumber,
        int totalCycles,
        int requestNumber,
        int requestsPerInterval,
        string? requestPayload,
        int sessionRequestIndex,
        int sessionTotalRequests,
        int payloadIndex,
        int payloadCount,
        RequestOutcome outcome);
}
