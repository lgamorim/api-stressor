namespace Stressor.Core.Tests;

public class JsonHttpHeadersReaderTests
{
    private readonly JsonHttpHeadersReader _reader = new();

    [Fact]
    public async Task Should_ReadHeaders_When_ValidObject()
    {
        var path = await WriteTempHeadersAsync("""{ "X-Api-Key": "abc", "Accept": "application/json" }""");

        var headers = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Equal("abc", headers["X-Api-Key"]);
        Assert.Equal("application/json", headers["Accept"]);
    }

    [Fact]
    public async Task Should_Throw_When_FileNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _reader.ReadAsync(path, TestCancellation.Token));
    }

    [Fact]
    public async Task Should_Throw_When_InvalidJson()
    {
        var path = await WriteTempHeadersAsync("{ not json");

        var exception = await Assert.ThrowsAsync<HttpHeadersValidationException>(() =>
            _reader.ReadAsync(path, TestCancellation.Token));

        Assert.Contains("valid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Throw_When_EmptyFile()
    {
        var path = await WriteTempHeadersAsync("   ");

        var exception = await Assert.ThrowsAsync<HttpHeadersValidationException>(() =>
            _reader.ReadAsync(path, TestCancellation.Token));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> WriteTempHeadersAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(path, content, TestCancellation.Token);
        return path;
    }
}
