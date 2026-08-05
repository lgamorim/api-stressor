namespace Stressor.App.UnitTests;

using Stressor.App;

public class StressorAppHelpTests
{
    [Fact]
    public void Should_IncludeExamplesOptionsAndExitCodes_When_WriteUsageGuide()
    {
        using var writer = new StringWriter();
        StressorAppHelp.WriteUsageGuide(writer);

        var help = writer.ToString();

        Assert.Contains("Examples:", help);
        Assert.Contains("--url", help);
        Assert.Contains("--payload", help);
        Assert.Contains("--requests", help);
        Assert.Contains("--interval", help);
        Assert.Contains("--cycles", help);
        Assert.Contains("--auth", help);
        Assert.Contains("--verbose", help);
        Assert.Contains("failures", help);
        Assert.Contains("full", help);
        Assert.DoesNotContain("--prettyprint", help);
        Assert.Contains("--load", help);
        Assert.Contains("gentle-pacing", help);
        Assert.Contains("fixed-rate", help);
        Assert.Contains("--batch", help);
        Assert.Contains("batch", help);
        Assert.Contains("--timeout", help);
        Assert.Contains("--cycle-interval", help);
        Assert.Contains("default: 100s", help);
        Assert.Contains("wave", help, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Supported HTTP methods:", help);
        Assert.Contains("Interval formats:", help);
        Assert.Contains("Exit codes:", help);
        Assert.Contains("  0  All requests completed successfully", help);
        Assert.Contains("  1  One or more requests failed, or arguments were invalid", help);
        Assert.Contains("  2  The session was cancelled", help);
    }
}
