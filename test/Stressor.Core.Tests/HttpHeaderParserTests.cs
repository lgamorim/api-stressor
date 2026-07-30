namespace Stressor.Core.Tests;

public class HttpHeaderParserTests
{
    [Fact]
    public void Should_ParseNameAndValue_When_ValidHeader()
    {
        Assert.True(HttpHeaderParser.TryParse("X-Api-Key: abc123", out var name, out var value));
        Assert.Equal("X-Api-Key", name);
        Assert.Equal("abc123", value);
    }

    [Fact]
    public void Should_ParseValueWithColon_When_EmbeddedColon()
    {
        Assert.True(HttpHeaderParser.TryParse("X-Trace: a:b:c", out var name, out var value));
        Assert.Equal("X-Trace", name);
        Assert.Equal("a:b:c", value);
    }

    [Fact]
    public void Should_TrimWhitespace_When_SpacesAroundValue()
    {
        Assert.True(HttpHeaderParser.TryParse("Accept:  application/json  ", out var name, out var value));
        Assert.Equal("Accept", name);
        Assert.Equal("application/json", value);
    }

    [Fact]
    public void Should_ReturnFalse_When_MissingColon()
    {
        Assert.False(HttpHeaderParser.TryParse("X-Api-Key", out _, out _));
    }

    [Fact]
    public void Should_ReturnFalse_When_EmptyName()
    {
        Assert.False(HttpHeaderParser.TryParse(": value", out _, out _));
    }
}
