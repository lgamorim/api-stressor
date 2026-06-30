namespace Stressor.Core;

public interface IJsonPayloadReader
{
    Task<IReadOnlyList<string>> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
