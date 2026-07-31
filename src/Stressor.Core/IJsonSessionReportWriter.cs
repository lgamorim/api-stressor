namespace Stressor.Core;

/// <summary>Writes session reports as JSON files.</summary>
public interface IJsonSessionReportWriter
{
    /// <summary>Serializes and writes a session report to the given file path.</summary>
    Task WriteAsync(
        SessionReport report,
        string filePath,
        int exitCode,
        string stressorVersion,
        CancellationToken cancellationToken = default);
}
