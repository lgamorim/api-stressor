namespace Stressor.Core;

public interface IConsoleSessionReporter
{
    void WriteSessionStart(StressTestOptions options);

    void WriteCycleSummary(int cycleNumber, int totalCycles, IReadOnlyList<RequestOutcome> cycleOutcomes);

    void WriteSessionComplete(SessionReport report);
}
