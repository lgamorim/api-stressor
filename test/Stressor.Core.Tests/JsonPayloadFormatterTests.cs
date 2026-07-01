namespace Stressor.Core.Tests;

public class JsonPayloadFormatterTests
{
    [Fact]
    public void PrettyPrint_CompactObject_IndentsWithAlignedBraces()
    {
        const string input = """{"foo":"bar"}""";
        const string expected = """
            {
              "foo": "bar"
            }
            """;

        var result = JsonPayloadFormatter.PrettyPrint(input);

        Assert.Equal(expected.Trim(), result);
    }

    [Fact]
    public void PrettyPrint_NestedObject_IndentsAllLevels()
    {
        const string input = """{"a":{"b":1}}""";
        const string expected = """
            {
              "a": {
                "b": 1
              }
            }
            """;

        var result = JsonPayloadFormatter.PrettyPrint(input);

        Assert.Equal(expected.Trim(), result);
    }

    [Fact]
    public void PrettyPrint_AlreadyIndentedInput_NormalizesOutput()
    {
        const string input = "{\n  \"a\": 1\n}";
        const string expected = """
            {
              "a": 1
            }
            """;

        var result = JsonPayloadFormatter.PrettyPrint(input);

        Assert.Equal(expected.Trim(), result);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("\"x\"")]
    [InlineData("null")]
    public void PrettyPrint_PrimitiveValues_ReturnsUnchanged(string input)
    {
        var result = JsonPayloadFormatter.PrettyPrint(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void PrettyPrint_InvalidJson_ReturnsInputUnchanged()
    {
        const string input = "{ not json }";

        var result = JsonPayloadFormatter.PrettyPrint(input);

        Assert.Equal(input, result);
    }
}
