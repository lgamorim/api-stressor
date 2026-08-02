namespace Stressor.Core.Tests;

public class StressTestOptionParserTests
{
    [Theory]
    [InlineData("1s", 1000)]
    [InlineData("500ms", 500)]
    [InlineData("00:00:01", 1000)]
    public void Should_ReturnTrue_When_TryParseIntervalValidValues(string value, double expectedMilliseconds)
    {
        Assert.True(StressTestOptionParser.TryParseInterval(value, out var interval));
        Assert.Equal(expectedMilliseconds, interval.TotalMilliseconds);
    }

    [Theory]
    [InlineData("0s")]
    [InlineData("0ms")]
    public void Should_AllowZero_When_BatchModeInterval(string value)
    {
        Assert.True(StressTestOptionParser.TryParseInterval(value, allowZero: true, out var interval));
        Assert.Equal(0, interval.TotalMilliseconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0s")]
    public void Should_ReturnFalse_When_TryParseIntervalInvalidValues(string value)
    {
        Assert.False(StressTestOptionParser.TryParseInterval(value, out _));
    }

    [Theory]
    [InlineData("gentle-pacing", LoadMode.GentlePacing)]
    [InlineData("fixed-rate", LoadMode.FixedRate)]
    [InlineData("batch", LoadMode.Batch)]
    public void Should_ReturnTrue_When_TryParseLoadModeValidValues(string value, LoadMode expected)
    {
        Assert.True(StressTestOptionParser.TryParseLoadMode(value, out var loadMode));
        Assert.Equal(expected, loadMode);
    }

    [Theory]
    [InlineData("burst")]
    [InlineData("")]
    public void Should_ReturnFalse_When_TryParseLoadModeInvalidValues(string value)
    {
        Assert.False(StressTestOptionParser.TryParseLoadMode(value, out _));
    }

    [Theory]
    [InlineData("failures", VerboseMode.Failures)]
    [InlineData("full", VerboseMode.Full)]
    public void Should_ParseKnownModes_When_TryParseVerboseMode(string value, VerboseMode expected)
    {
        Assert.True(StressTestOptionParser.TryParseVerboseMode(value, out var mode));
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void Should_ParseOff_When_VerboseNull()
    {
        Assert.True(StressTestOptionParser.TryParseVerboseMode(null, out var mode));
        Assert.Equal(VerboseMode.Off, mode);
    }

    [Fact]
    public void Should_ReturnFalse_When_VerboseUnknown()
    {
        Assert.False(StressTestOptionParser.TryParseVerboseMode("unknown", out _));
    }

    [Fact]
    public void Should_CreateOptions_When_ValidConfiguration()
    {
        var values = new StressTestConfigurationValues
        {
            Url = "https://example.com/api",
            Payload = "payload.json",
            Method = "POST",
            Requests = 10,
            Interval = "1s",
            Cycles = 5
        };

        var (options, errors) = StressTestOptionParser.TryCreateOptions(values);

        Assert.NotNull(options);
        Assert.Empty(errors);
        Assert.Equal(new Uri("https://example.com/api"), options.Url);
        Assert.Equal("payload.json", options.PayloadFilePath);
        Assert.Equal(10, options.RequestsPerInterval);
    }

    [Fact]
    public void Should_ReturnErrors_When_RequiredFieldsMissing()
    {
        var (options, errors) = StressTestOptionParser.TryCreateOptions(new StressTestConfigurationValues());

        Assert.Null(options);
        Assert.Contains(errors, e => e.Contains("URL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Requests", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("5m", 300_000)]
    [InlineData("300s", 300_000)]
    [InlineData("00:05:00", 300_000)]
    public void Should_ParseDuration_When_ValidFormats(string value, double expectedMilliseconds)
    {
        Assert.True(StressTestOptionParser.TryParseInterval(value, out var interval));
        Assert.Equal(expectedMilliseconds, interval.TotalMilliseconds);
    }

    [Fact]
    public void Should_CreateOptionsWithDuration_When_ValidConfiguration()
    {
        var values = CreateValidValues() with
        {
            Duration = "5m",
            DurationSpecified = true,
            CyclesSpecified = false
        };

        var (options, errors) = StressTestOptionParser.TryCreateOptions(values);

        Assert.NotNull(options);
        Assert.Empty(errors);
        Assert.True(options.IsDurationLimited);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Duration);
    }

    [Fact]
    public void Should_CreateOptionsWithProgress_When_ValidConfiguration()
    {
        var values = CreateValidValues() with { Progress = true, ProgressSpecified = true };

        var (options, errors) = StressTestOptionParser.TryCreateOptions(values);

        Assert.NotNull(options);
        Assert.Empty(errors);
        Assert.True(options.Progress);
    }

    [Fact]
    public void Should_ReturnError_When_DurationAndCyclesBothSpecified()
    {
        var values = CreateValidValues() with
        {
            CyclesSpecified = true,
            Duration = "5m",
            DurationSpecified = true
        };

        var (options, errors) = StressTestOptionParser.TryCreateOptions(values);

        Assert.Null(options);
        Assert.Contains(errors, e => e.Contains("duration", StringComparison.OrdinalIgnoreCase)
            && e.Contains("cycles", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("0s")]
    [InlineData("-1s")]
    public void Should_ReturnError_When_DurationZeroOrNegative(string duration)
    {
        var values = CreateValidValues() with
        {
            Duration = duration,
            DurationSpecified = true,
            CyclesSpecified = false
        };

        var (options, errors) = StressTestOptionParser.TryCreateOptions(values);

        Assert.Null(options);
        Assert.Contains(errors, e => e.Contains("Duration", StringComparison.OrdinalIgnoreCase));
    }

    private static StressTestConfigurationValues CreateValidValues() =>
        new()
        {
            Url = "https://example.com/api",
            Payload = "payload.json",
            Method = "POST",
            Requests = 10,
            Interval = "1s",
            Cycles = 5
        };
}
