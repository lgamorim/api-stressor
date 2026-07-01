namespace Stressor.Core.Tests;

public class StressTestOptionsValidatorTests
{
    private static StressTestOptions CreateValidOptions() =>
        new(new Uri("https://example.com/api"), "payload.json", HttpMethod.Post, 10, TimeSpan.FromSeconds(1), 5);

    [Fact]
    public void Validate_ValidOptions_ReturnsNoErrors()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions());

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NonAbsoluteUrl_ReturnsError()
    {
        var options = CreateValidOptions() with { Url = new Uri("/relative", UriKind.Relative) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("absolute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NonHttpScheme_ReturnsError()
    {
        var options = CreateValidOptions() with { Url = new Uri("ftp://example.com") };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("http", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ZeroRequests_ReturnsError()
    {
        var options = CreateValidOptions() with { RequestsPerInterval = 0 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Requests per interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeCycles_ReturnsError()
    {
        var options = CreateValidOptions() with { Cycles = -1 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Cycles", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_UnknownHttpMethod_ReturnsError()
    {
        var options = CreateValidOptions() with { Method = new HttpMethod("INVALID") };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("INVALID", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Allowed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WhitespaceAuth_ReturnsError()
    {
        var options = CreateValidOptions() with { Auth = "   " };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Auth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AuthOmitted_ReturnsNoErrors()
    {
        var options = CreateValidOptions() with { Auth = null };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ZeroInterval_ReturnsError()
    {
        var options = CreateValidOptions() with { Interval = TimeSpan.Zero };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeInterval_ReturnsError()
    {
        var options = CreateValidOptions() with { Interval = TimeSpan.FromSeconds(-1) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_EmptyPayloadPath_ReturnsError()
    {
        var options = CreateValidOptions() with { PayloadFilePath = "" };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WhitespacePayloadPath_ReturnsError()
    {
        var options = CreateValidOptions() with { PayloadFilePath = "   " };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ZeroCycles_ReturnsError()
    {
        var options = CreateValidOptions() with { Cycles = 0 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Cycles", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeRequests_ReturnsError()
    {
        var options = CreateValidOptions() with { RequestsPerInterval = -1 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Requests per interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MultipleInvalidFields_ReturnsAllErrors()
    {
        var options = CreateValidOptions() with
        {
            RequestsPerInterval = 0,
            Interval = TimeSpan.Zero
        };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.True(errors.Count >= 2);
        Assert.Contains(errors, e => e.Contains("Requests per interval", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }
}
