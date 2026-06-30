namespace Stressor.Core.Tests;

public class JsonPayloadReaderTests
{
    private readonly JsonPayloadReader reader = new();

    [Fact]
    public async Task ReadAsync_ValidJsonObject_ReturnsSingleItemList()
    {
        var path = await WriteTempFileAsync("{\"name\":\"test\"}");

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal("{\"name\":\"test\"}", payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_ValidJsonArray_ReturnsSingleItemList()
    {
        var path = await WriteTempFileAsync("[1,2,3]");

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal("[1,2,3]", payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_ValidNestedJson_ReturnsSingleItemList()
    {
        var json = "{\"items\":[{\"id\":1},{\"id\":2}],\"meta\":{\"count\":2}}";
        var path = await WriteTempFileAsync(json);

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_RootStringPrimitive_ReturnsSingleItemList()
    {
        var path = await WriteTempFileAsync("\"hello\"");

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal("\"hello\"", payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_RootNull_ReturnsSingleItemList()
    {
        var path = await WriteTempFileAsync("null");

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal("null", payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_EmptyObject_ReturnsSingleItemList()
    {
        var path = await WriteTempFileAsync("{}");

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal("{}", payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_ObjectWithPayloadsAndOtherFields_ReturnsSingleItemList()
    {
        var json = "{\"orderId\":1,\"payloads\":[1,2]}";
        var path = await WriteTempFileAsync(json);

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_WrongCasePayloadsKey_ReturnsSingleItemList()
    {
        var json = "{\"Payloads\":[{\"id\":1}]}";
        var path = await WriteTempFileAsync(json);

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_TwoRootPropertiesIncludingPayloads_ReturnsSingleItemList()
    {
        var json = "{\"payloads\":[{\"id\":1}],\"extra\":1}";
        var path = await WriteTempFileAsync(json);

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_PreservesWhitespace_ReturnsSingleItemList()
    {
        var json = "{\n  \"name\": \"test\"\n}";
        var path = await WriteTempFileAsync(json);

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_EnvelopeWithObjects_ReturnsSeparatePayloads()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[{\"id\":1},{\"id\":2}]}");

        var payloads = await reader.ReadAsync(path);

        Assert.Equal(2, payloads.Count);
        Assert.Equal("{\"id\":1}", payloads[0]);
        Assert.Equal("{\"id\":2}", payloads[1]);
    }

    [Fact]
    public async Task ReadAsync_EnvelopeWithSingleItem_ReturnsOneItemList()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[{\"id\":1}]}");

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal("{\"id\":1}", payloads[0]);
    }

    [Fact]
    public async Task ReadAsync_EnvelopeWithMixedElements_ReturnsEachRawText()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[{\"id\":1},[1,2],\"text\",42,null]}");

        var payloads = await reader.ReadAsync(path);

        Assert.Equal(5, payloads.Count);
        Assert.Equal("{\"id\":1}", payloads[0]);
        Assert.Equal("[1,2]", payloads[1]);
        Assert.Equal("\"text\"", payloads[2]);
        Assert.Equal("42", payloads[3]);
        Assert.Equal("null", payloads[4]);
    }

    [Fact]
    public async Task ReadAsync_EnvelopeWithNestedArrayElement_ReturnsElementRawText()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[[1,2]]}");

        var payloads = await reader.ReadAsync(path);

        Assert.Single(payloads);
        Assert.Equal("[1,2]", payloads[0]);
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

    [Fact]
    public async Task ReadAsync_EmptyPayloadsArray_Throws()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[]}");

        var exception = await Assert.ThrowsAsync<JsonPayloadValidationException>(() => reader.ReadAsync(path));

        Assert.Equal("Payload array is empty.", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_PayloadsNull_Throws()
    {
        var path = await WriteTempFileAsync("{\"payloads\":null}");

        var exception = await Assert.ThrowsAsync<JsonPayloadValidationException>(() => reader.ReadAsync(path));

        Assert.Equal("The payloads property must be a JSON array.", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_PayloadsNotArray_Throws()
    {
        var path = await WriteTempFileAsync("{\"payloads\":\"text\"}");

        var exception = await Assert.ThrowsAsync<JsonPayloadValidationException>(() => reader.ReadAsync(path));

        Assert.Equal("The payloads property must be a JSON array.", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_PayloadsObject_Throws()
    {
        var path = await WriteTempFileAsync("{\"payloads\":{}}");

        var exception = await Assert.ThrowsAsync<JsonPayloadValidationException>(() => reader.ReadAsync(path));

        Assert.Equal("The payloads property must be a JSON array.", exception.Message);
    }

    private static async Task<string> WriteTempFileAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
