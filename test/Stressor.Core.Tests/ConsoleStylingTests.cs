namespace Stressor.Core.Tests;

public class ConsoleStylingTests
{
    [Fact]
    public void Should_ReturnPlainErrorPrefix_When_StringWriterOutput()
    {
        using var writer = new StringWriter();

        var prefix = ConsoleStyling.FormatErrorPrefix(writer);

        Assert.Equal("Fail: ", prefix);
    }

    [Fact]
    public void Should_ReturnPlainSuccessPrefix_When_StringWriterOutput()
    {
        using var writer = new StringWriter();

        var prefix = ConsoleStyling.FormatSuccessPrefix(writer);

        Assert.Equal("OK: ", prefix);
    }
}
