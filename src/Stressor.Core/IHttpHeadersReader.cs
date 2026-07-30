namespace Stressor.Core;

/// <summary>Reads HTTP headers from a JSON file.</summary>
public interface IHttpHeadersReader
{
    /// <summary>Reads and parses a JSON object of header name/value pairs.</summary>
    Task<IReadOnlyDictionary<string, string>> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
