namespace Stressor.Core;

using System.Text.Json;

/// <summary>Writes session reports as indented JSON files.</summary>
public sealed class JsonSessionReportWriter : IJsonSessionReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a writer that uses the given time provider for completion timestamps.</summary>
    public JsonSessionReportWriter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        SessionReport report,
        string filePath,
        int exitCode,
        string stressorVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Report file path is required.", nameof(filePath));
        }

        var document = SessionReportMapper.ToDocument(
            report,
            exitCode,
            stressorVersion,
            _timeProvider.GetUtcNow());

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
