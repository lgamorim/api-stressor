namespace Stressor.Core.Tests;

public class JsonPayloadReaderTests
{
    private readonly JsonPayloadReader _reader = new();

    [Fact]
    public async Task Should_ReturnSingleItemList_When_ValidJsonObject()
    {
        var path = await WriteTempFileAsync("{\"name\":\"test\"}");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal("{\"name\":\"test\"}", payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_ValidJsonArray()
    {
        var path = await WriteTempFileAsync("[1,2,3]");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal("[1,2,3]", payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_ValidNestedJson()
    {
        var json = "{\"items\":[{\"id\":1},{\"id\":2}],\"meta\":{\"count\":2}}";
        var path = await WriteTempFileAsync(json);

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_RootStringPrimitive()
    {
        var path = await WriteTempFileAsync("\"hello\"");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal("\"hello\"", payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_RootNull()
    {
        var path = await WriteTempFileAsync("null");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal("null", payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_EmptyObject()
    {
        var path = await WriteTempFileAsync("{}");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal("{}", payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_ObjectWithPayloadsAndOtherFields()
    {
        var json = "{\"orderId\":1,\"payloads\":[1,2]}";
        var path = await WriteTempFileAsync(json);

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_WrongCasePayloadsKey()
    {
        var json = "{\"Payloads\":[{\"id\":1}]}";
        var path = await WriteTempFileAsync(json);

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_TwoRootPropertiesIncludingPayloads()
    {
        var json = "{\"payloads\":[{\"id\":1}],\"extra\":1}";
        var path = await WriteTempFileAsync(json);

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSingleItemList_When_PreserveWhitespace()
    {
        var json = "{\n  \"name\": \"test\"\n}";
        var path = await WriteTempFileAsync(json);

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal(json, payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnSeparatePayloads_When_EnvelopeWithObjects()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[{\"id\":1},{\"id\":2}]}");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Equal(2, payloads.Count);
        Assert.Equal("{\"id\":1}", payloads[0]);
        Assert.Equal("{\"id\":2}", payloads[1]);
    }

    [Fact]
    public async Task Should_ReturnOneItemList_When_EnvelopeWithSingleItem()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[{\"id\":1}]}");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal("{\"id\":1}", payloads[0]);
    }

    [Fact]
    public async Task Should_ReturnEachRawText_When_EnvelopeWithMixedElements()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[{\"id\":1},[1,2],\"text\",42,null]}");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Equal(5, payloads.Count);
        Assert.Equal("{\"id\":1}", payloads[0]);
        Assert.Equal("[1,2]", payloads[1]);
        Assert.Equal("\"text\"", payloads[2]);
        Assert.Equal("42", payloads[3]);
        Assert.Equal("null", payloads[4]);
    }

    [Fact]
    public async Task Should_ReturnElementRawText_When_EnvelopeWithNestedArrayElement()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[[1,2]]}");

        var payloads = await _reader.ReadAsync(path, TestCancellation.Token);

        Assert.Single(payloads);
        Assert.Equal("[1,2]", payloads[0]);
    }

    [Fact]
    public async Task Should_ThrowFileNotFoundException_When_FileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _reader.ReadAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), TestCancellation.Token));
    }

    [Fact]
    public async Task Should_ThrowJsonPayloadValidationException_When_InvalidJson()
    {
        var path = await WriteTempFileAsync("{ invalid }");

        await Assert.ThrowsAsync<JsonPayloadValidationException>(() => _reader.ReadAsync(path, TestCancellation.Token));
    }

    [Fact]
    public async Task Should_ThrowJsonPayloadValidationException_When_EmptyFile()
    {
        var path = await WriteTempFileAsync(string.Empty);

        await Assert.ThrowsAsync<JsonPayloadValidationException>(() => _reader.ReadAsync(path, TestCancellation.Token));
    }

    [Fact]
    public async Task Should_ThrowJsonPayloadValidationException_When_WhitespaceOnlyFile()
    {
        var path = await WriteTempFileAsync("   \t\n  ");

        await Assert.ThrowsAsync<JsonPayloadValidationException>(() => _reader.ReadAsync(path, TestCancellation.Token));
    }

    [Fact]
    public async Task Should_Throw_When_EmptyPayloadsArray()
    {
        var path = await WriteTempFileAsync("{\"payloads\":[]}");

        var exception = await Assert.ThrowsAsync<JsonPayloadValidationException>(() => _reader.ReadAsync(path, TestCancellation.Token));

        Assert.Equal("Payload array is empty.", exception.Message);
    }

    [Fact]
    public async Task Should_Throw_When_PayloadsNull()
    {
        var path = await WriteTempFileAsync("{\"payloads\":null}");

        var exception = await Assert.ThrowsAsync<JsonPayloadValidationException>(() => _reader.ReadAsync(path, TestCancellation.Token));

        Assert.Equal("The payloads property must be a JSON array.", exception.Message);
    }

    [Fact]
    public async Task Should_Throw_When_PayloadsNotArray()
    {
        var path = await WriteTempFileAsync("{\"payloads\":\"text\"}");

        var exception = await Assert.ThrowsAsync<JsonPayloadValidationException>(() => _reader.ReadAsync(path, TestCancellation.Token));

        Assert.Equal("The payloads property must be a JSON array.", exception.Message);
    }

    [Fact]
    public async Task Should_Throw_When_PayloadsObject()
    {
        var path = await WriteTempFileAsync("{\"payloads\":{}}");

        var exception = await Assert.ThrowsAsync<JsonPayloadValidationException>(() => _reader.ReadAsync(path, TestCancellation.Token));

        Assert.Equal("The payloads property must be a JSON array.", exception.Message);
    }

    private static async Task<string> WriteTempFileAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(path, content, TestCancellation.Token);
        return path;
    }
}
