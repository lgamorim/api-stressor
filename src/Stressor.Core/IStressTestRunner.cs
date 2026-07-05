namespace Stressor.Core;

public interface IStressTestRunner
{
    Task<SessionReport> RunAsync(StressTestOptions options, CancellationToken cancellationToken = default);
}
