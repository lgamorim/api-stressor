namespace Stressor.Core;

using System.Text.Json;

/// <summary>Reads HTTP headers from a JSON file.</summary>
public sealed class JsonHttpHeadersReader : IHttpHeadersReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Headers file not found: {filePath}", filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new HttpHeadersValidationException("Headers file is empty or contains only whitespace.");
        }

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(content, SerializerOptions);
            if (headers is null || headers.Count == 0)
            {
                throw new HttpHeadersValidationException("Headers file does not contain a JSON object with header entries.");
            }

            return new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            throw new HttpHeadersValidationException("Headers file does not contain valid JSON.", ex);
        }
    }
}
