namespace Stressor.Core;

/// <summary>Reads stress-test scenario configuration from disk.</summary>
public interface IStressTestScenarioReader
{
    /// <summary>Reads and parses a JSON scenario file.</summary>
    Task<StressTestScenarioDocument> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
