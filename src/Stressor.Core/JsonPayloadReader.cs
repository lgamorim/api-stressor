namespace Stressor.Core;

using System.Text.Json;

public sealed class JsonPayloadReader : IJsonPayloadReader
{
    public async Task<IReadOnlyList<string>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
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

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new JsonPayloadValidationException("Payload file does not contain valid JSON.", ex);
        }

        using (document)
        {
            if (TryReadEnvelope(document.RootElement, out var payloads))
            {
                return payloads;
            }

            return [content];
        }
    }

    private static bool TryReadEnvelope(JsonElement root, out IReadOnlyList<string> payloads)
    {
        payloads = [];

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.GetPropertyCount() != 1 || !root.TryGetProperty("payloads", out var payloadsElement))
        {
            return false;
        }

        if (payloadsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonPayloadValidationException("The payloads property must be a JSON array.");
        }

        var items = new List<string>();
        foreach (var element in payloadsElement.EnumerateArray())
        {
            items.Add(element.GetRawText());
        }

        if (items.Count == 0)
        {
            throw new JsonPayloadValidationException("Payload array is empty.");
        }

        payloads = items;
        return true;
    }
}
