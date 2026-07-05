namespace Stressor.Core;

public interface IConsoleSessionReporter
{
    void WriteSessionStart(StressTestOptions options);

    void WriteCycleSummary(int cycleNumber, int totalCycles, IReadOnlyList<RequestOutcome> cycleOutcomes);

    void WriteSessionComplete(SessionReport report);

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
