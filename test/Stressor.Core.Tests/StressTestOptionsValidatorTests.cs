namespace Stressor.Core.Tests;

public class StressTestOptionsValidatorTests
{
    private static StressTestOptions CreateValidOptions(
        int requests = 10,
        LoadMode load = LoadMode.GentlePacing,
        int batch = 1,
        TimeSpan? interval = null) =>
        new(
            new Uri("https://example.com/api"),
            "payload.json",
            HttpMethod.Post,
            requests,
            interval ?? TimeSpan.FromSeconds(1),
            5,
            Load: load,
            Batch: batch);

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
    public void Validate_OneCycle_ReturnsNoErrors()
    {
        var options = CreateValidOptions() with { Cycles = 1 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
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

    [Fact]
    public void Validate_BatchLoadWithValidBatch_ReturnsNoErrors()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.Batch, batch: 5));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_BatchLoadWithBatchOne_ReturnsNoErrors()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.Batch, batch: 1));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ZeroBatch_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: 0));

        Assert.Contains(errors, e => e.Contains("Batch size", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeBatch_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: -1));

        Assert.Contains(errors, e => e.Contains("Batch size", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BatchGreaterThanRequests_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(requests: 10, load: LoadMode.Batch, batch: 11));

        Assert.Contains(errors, e => e.Equals("Batch size cannot be greater than requests per cycle.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BatchEqualToRequests_ReturnsNoErrors()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(requests: 10, load: LoadMode.Batch, batch: 10));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_BatchGreaterThanRequests_WithGentlePacing_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(requests: 3, batch: 5));

        Assert.Contains(errors, e => e.Equals("Batch size cannot be greater than requests per cycle.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BatchGreaterThanOneWithGentlePacing_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: 5));

        Assert.Contains(errors, e => e.Equals("Use --load batch when --batch is greater than 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BatchGreaterThanOneWithFixedRate_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.FixedRate, batch: 5));

        Assert.Contains(errors, e => e.Equals("Use --load batch when --batch is greater than 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BatchLoadWithZeroInterval_ReturnsNoErrors()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.Batch, batch: 5, interval: TimeSpan.Zero));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_BatchLoadWithNegativeInterval_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.Batch, batch: 5, interval: TimeSpan.FromSeconds(-1)));

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_GentlePacingWithZeroInterval_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(interval: TimeSpan.Zero));

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_FixedRateWithZeroInterval_ReturnsError()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.FixedRate, interval: TimeSpan.Zero));

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BatchOneWithGentlePacing_ReturnsNoErrors()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: 1, load: LoadMode.GentlePacing));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_BatchOneWithFixedRate_ReturnsNoErrors()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: 1, load: LoadMode.FixedRate));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_LoadBatchWithBatchGreaterThanOne_ReturnsNoErrors()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(requests: 10, load: LoadMode.Batch, batch: 5));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MultipleInvalidFields_IncludesBatchErrors()
    {
        var options = CreateValidOptions(requests: 10, batch: 11) with { Load = LoadMode.GentlePacing };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.True(errors.Count >= 2);
        Assert.Contains(errors, e => e.Equals("Batch size cannot be greater than requests per cycle.", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Equals("Use --load batch when --batch is greater than 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ZeroRequestTimeout_ReturnsError()
    {
        var options = CreateValidOptions() with { RequestTimeout = TimeSpan.Zero };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Equals("Request timeout must be greater than zero.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NegativeRequestTimeout_ReturnsError()
    {
        var options = CreateValidOptions() with { RequestTimeout = TimeSpan.FromSeconds(-1) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Equals("Request timeout must be greater than zero.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_CustomRequestTimeout_ReturnsNoErrors()
    {
        var options = CreateValidOptions() with { RequestTimeout = TimeSpan.FromMinutes(2) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ZeroCycleInterval_ReturnsNoErrors()
    {
        var options = CreateValidOptions() with { CycleInterval = TimeSpan.Zero };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_PositiveCycleInterval_ReturnsNoErrors()
    {
        var options = CreateValidOptions() with { CycleInterval = TimeSpan.FromSeconds(30) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NegativeCycleInterval_ReturnsError()
    {
        var options = CreateValidOptions() with { CycleInterval = TimeSpan.FromSeconds(-1) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Equals("Cycle interval must be greater than or equal to zero.", StringComparison.Ordinal));
    }
}
