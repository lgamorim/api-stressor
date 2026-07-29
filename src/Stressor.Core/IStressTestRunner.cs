namespace Stressor.Core;

/// <summary>Executes a configured stress-test session.</summary>
public interface IStressTestRunner
{
    /// <summary>Runs the stress session and returns the aggregated report.</summary>
    Task<SessionReport> RunAsync(StressTestOptions options, CancellationToken cancellationToken = default);
}
