namespace Stressor.Core;

using System.Text.Json;

public sealed class JsonPayloadReader : IJsonPayloadReader
{
    public async Task<string> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Payload file not found: {filePath}", filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new JsonPayloadValidationException("Payload file is empty or contains only whitespace.");
        }

        try
        {
            using var document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new JsonPayloadValidationException("Payload file does not contain valid JSON.", ex);
        }

        return content;
    }
}
