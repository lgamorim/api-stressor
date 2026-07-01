namespace Stressor.App.Tests;

using Stressor.App;

public class StressorAppHelpTests
{
    [Fact]
    public void WriteUsageGuide_IncludesExamplesOptionsAndExitCodes()
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
        Assert.Contains("--prettyprint", help);
        Assert.Contains("success latency", help, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Supported HTTP methods:", help);
        Assert.Contains("Interval formats:", help);
        Assert.Contains("Exit codes:", help);
        Assert.Contains("  0  All requests completed successfully", help);
        Assert.Contains("  1  One or more requests failed, or arguments were invalid", help);
        Assert.Contains("  2  The session was cancelled", help);
    }
}
