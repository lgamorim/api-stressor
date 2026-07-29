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
    public void Should_ReturnNoErrors_When_ValidOptions()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions());

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnError_When_NonAbsoluteUrl()
    {
        var options = CreateValidOptions() with { Url = new Uri("/relative", UriKind.Relative) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("absolute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_NonHttpScheme()
    {
        var options = CreateValidOptions() with { Url = new Uri("ftp://example.com") };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("http", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_ZeroRequests()
    {
        var options = CreateValidOptions() with { RequestsPerInterval = 0 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Requests per interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_NegativeCycles()
    {
        var options = CreateValidOptions() with { Cycles = -1 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Cycles", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_UnknownHttpMethod()
    {
        var options = CreateValidOptions() with { Method = new HttpMethod("INVALID") };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("INVALID", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Allowed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_WhitespaceAuth()
    {
        var options = CreateValidOptions() with { Auth = "   " };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Auth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnNoErrors_When_AuthOmitted()
    {
        var options = CreateValidOptions() with { Auth = null };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnError_When_ZeroInterval()
    {
        var options = CreateValidOptions() with { Interval = TimeSpan.Zero };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_NegativeInterval()
    {
        var options = CreateValidOptions() with { Interval = TimeSpan.FromSeconds(-1) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_EmptyPayloadPath()
    {
        var options = CreateValidOptions() with { PayloadFilePath = "" };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_WhitespacePayloadPath()
    {
        var options = CreateValidOptions() with { PayloadFilePath = "   " };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_ZeroCycles()
    {
        var options = CreateValidOptions() with { Cycles = 0 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Cycles", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnNoErrors_When_OneCycle()
    {
        var options = CreateValidOptions() with { Cycles = 1 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnError_When_NegativeRequests()
    {
        var options = CreateValidOptions() with { RequestsPerInterval = -1 };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("Requests per interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnAllErrors_When_MultipleInvalidFields()
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
    public void Should_ReturnNoErrors_When_BatchLoadWithValidBatch()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.Batch, batch: 5));

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_BatchLoadWithBatchOne()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.Batch, batch: 1));

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnError_When_ZeroBatch()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: 0));

        Assert.Contains(errors, e => e.Contains("Batch size", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_NegativeBatch()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: -1));

        Assert.Contains(errors, e => e.Contains("Batch size", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_BatchGreaterThanRequests()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(requests: 10, load: LoadMode.Batch, batch: 11));

        Assert.Contains(errors, e => e.Equals("Batch size cannot be greater than requests per cycle.", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_ReturnNoErrors_When_BatchEqualToRequests()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(requests: 10, load: LoadMode.Batch, batch: 10));

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnError_When_BatchGreaterThanRequests_WithGentlePacing()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(requests: 3, batch: 5));

        Assert.Contains(errors, e => e.Equals("Batch size cannot be greater than requests per cycle.", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_ReturnError_When_BatchGreaterThanOneWithGentlePacing()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: 5));

        Assert.Contains(errors, e => e.Equals("Use --load batch when --batch is greater than 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_ReturnError_When_BatchGreaterThanOneWithFixedRate()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.FixedRate, batch: 5));

        Assert.Contains(errors, e => e.Equals("Use --load batch when --batch is greater than 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_ReturnNoErrors_When_BatchLoadWithZeroInterval()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.Batch, batch: 5, interval: TimeSpan.Zero));

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnError_When_BatchLoadWithNegativeInterval()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.Batch, batch: 5, interval: TimeSpan.FromSeconds(-1)));

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_GentlePacingWithZeroInterval()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(interval: TimeSpan.Zero));

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnError_When_FixedRateWithZeroInterval()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(load: LoadMode.FixedRate, interval: TimeSpan.Zero));

        Assert.Contains(errors, e => e.Contains("Interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReturnNoErrors_When_BatchOneWithGentlePacing()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: 1, load: LoadMode.GentlePacing));

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_BatchOneWithFixedRate()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(batch: 1, load: LoadMode.FixedRate));

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_LoadBatchWithBatchGreaterThanOne()
    {
        var errors = StressTestOptionsValidator.Validate(CreateValidOptions(requests: 10, load: LoadMode.Batch, batch: 5));

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_IncludeBatchErrors_When_MultipleInvalidFields()
    {
        var options = CreateValidOptions(requests: 10, batch: 11) with { Load = LoadMode.GentlePacing };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.True(errors.Count >= 2);
        Assert.Contains(errors, e => e.Equals("Batch size cannot be greater than requests per cycle.", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Equals("Use --load batch when --batch is greater than 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_ReturnError_When_ZeroRequestTimeout()
    {
        var options = CreateValidOptions() with { RequestTimeout = TimeSpan.Zero };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Equals("Request timeout must be greater than zero.", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_ReturnError_When_NegativeRequestTimeout()
    {
        var options = CreateValidOptions() with { RequestTimeout = TimeSpan.FromSeconds(-1) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Equals("Request timeout must be greater than zero.", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_ReturnNoErrors_When_CustomRequestTimeout()
    {
        var options = CreateValidOptions() with { RequestTimeout = TimeSpan.FromMinutes(2) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_ZeroCycleInterval()
    {
        var options = CreateValidOptions() with { CycleInterval = TimeSpan.Zero };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnNoErrors_When_PositiveCycleInterval()
    {
        var options = CreateValidOptions() with { CycleInterval = TimeSpan.FromSeconds(30) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Should_ReturnError_When_NegativeCycleInterval()
    {
        var options = CreateValidOptions() with { CycleInterval = TimeSpan.FromSeconds(-1) };

        var errors = StressTestOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Equals("Cycle interval must be greater than or equal to zero.", StringComparison.Ordinal));
    }
}
