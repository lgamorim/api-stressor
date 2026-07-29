namespace Stressor.Core;

/// <summary>Reads JSON payload file content for stress requests.</summary>
public interface IJsonPayloadReader
{
    /// <summary>Reads payloads from a JSON file (single body or multi-payload envelope).</summary>
    Task<IReadOnlyList<string>> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
