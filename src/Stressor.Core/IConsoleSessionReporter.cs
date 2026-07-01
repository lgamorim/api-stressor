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
        string payload,
        bool prettyPrint,
        RequestOutcome outcome);
}
