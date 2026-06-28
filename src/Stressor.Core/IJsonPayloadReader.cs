namespace Stressor.Core;

public interface IJsonPayloadReader
{
    Task<string> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
