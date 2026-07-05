namespace Stressor.Core.Tests;

public class ConsoleStylingTests
{
    [Fact]
    public void FormatErrorPrefix_StringWriterOutput_ReturnsPlainPrefix()
    {
        using var writer = new StringWriter();

        var prefix = ConsoleStyling.FormatErrorPrefix(writer);

        Assert.Equal("Fail: ", prefix);
    }

    [Fact]
    public void FormatSuccessPrefix_StringWriterOutput_ReturnsPlainPrefix()
    {
        using var writer = new StringWriter();

        var prefix = ConsoleStyling.FormatSuccessPrefix(writer);

        Assert.Equal("OK: ", prefix);
    }
}
