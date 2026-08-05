namespace Stressor.Core.UnitTests;

public class ExpectedStatusCodeParserTests
{
    [Theory]
    [InlineData("200", 200)]
    [InlineData(" 201 ", 201)]
    [InlineData("100", 100)]
    [InlineData("599", 599)]
    public void Should_ParseCode_When_ValidInput(string input, int expected)
    {
        Assert.True(ExpectedStatusCodeParser.TryParse(input, out var code));
        Assert.Equal(expected, code);
    }

    [Fact]
    public void Should_ParseManyCodes_When_CommaSeparated()
    {
        Assert.True(ExpectedStatusCodeParser.TryParseMany(["200,201", "204"], out var codes, out var error));
        Assert.Null(error);
        Assert.Equal([200, 201, 204], codes.OrderBy(c => c));
    }

    [Fact]
    public void Should_ParseManyCodes_When_SingleValues()
    {
        Assert.True(ExpectedStatusCodeParser.TryParseMany(["200", "201"], out var codes, out var error));
        Assert.Null(error);
        Assert.Equal([200, 201], codes.OrderBy(c => c));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("99")]
    [InlineData("600")]
    public void Should_ReturnFalse_When_InvalidSingleCode(string input)
    {
        Assert.False(ExpectedStatusCodeParser.TryParse(input, out _));
    }

    [Fact]
    public void Should_ReturnFalseWithError_When_InvalidEntryInMany()
    {
        Assert.False(ExpectedStatusCodeParser.TryParseMany(["200", "abc"], out _, out var error));
        Assert.NotNull(error);
    }
}
