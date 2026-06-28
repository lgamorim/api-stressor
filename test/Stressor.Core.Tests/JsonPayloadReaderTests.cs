namespace Stressor.Core.Tests;

public class JsonPayloadReaderTests
{
    private readonly JsonPayloadReader reader = new();

    [Fact]
    public async Task ReadAsync_ValidJsonObject_ReturnsContentUnchanged()
    {
        var path = await WriteTempFileAsync("{\"name\":\"test\"}");

        var content = await reader.ReadAsync(path);

        Assert.Equal("{\"name\":\"test\"}", content);
    }

    [Fact]
    public async Task ReadAsync_ValidJsonArray_ReturnsContentUnchanged()
    {
        var path = await WriteTempFileAsync("[1,2,3]");

        var content = await reader.ReadAsync(path);

        Assert.Equal("[1,2,3]", content);
    }

    [Fact]
    public async Task ReadAsync_ValidNestedJson_ParsesSuccessfully()
    {
        var json = "{\"items\":[{\"id\":1},{\"id\":2}],\"meta\":{\"count\":2}}";
        var path = await WriteTempFileAsync(json);

        var content = await reader.ReadAsync(path);

        Assert.Equal(json, content);
    }

    [Fact]
    public async Task ReadAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            reader.ReadAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));
    }

    [Fact]
    public async Task ReadAsync_InvalidJson_ThrowsJsonPayloadValidationException()
    {
        var path = await WriteTempFileAsync("{ invalid }");

        await Assert.ThrowsAsync<JsonPayloadValidationException>(() => reader.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_EmptyFile_ThrowsJsonPayloadValidationException()
    {
        var path = await WriteTempFileAsync(string.Empty);

        await Assert.ThrowsAsync<JsonPayloadValidationException>(() => reader.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_WhitespaceOnlyFile_ThrowsJsonPayloadValidationException()
    {
        var path = await WriteTempFileAsync("   \t\n  ");

        await Assert.ThrowsAsync<JsonPayloadValidationException>(() => reader.ReadAsync(path));
    }

    private static async Task<string> WriteTempFileAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
