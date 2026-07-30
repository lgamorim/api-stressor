namespace Stressor.Core;

using System.Text.Json;

/// <summary>Reads JSON stress-test scenario files from disk.</summary>
public sealed class JsonStressTestScenarioReader : IStressTestScenarioReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<StressTestScenarioDocument> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Scenario config file not found: {filePath}", filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new StressTestScenarioValidationException("Scenario config file is empty or contains only whitespace.");
        }

        try
        {
            var document = JsonSerializer.Deserialize<StressTestScenarioDocument>(content, SerializerOptions);
            return document ?? throw new StressTestScenarioValidationException("Scenario config file does not contain a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new StressTestScenarioValidationException("Scenario config file does not contain valid JSON.", ex);
        }
    }
}
