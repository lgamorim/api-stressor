namespace Stressor.Core.Tests;

using System.Globalization;

public class ConsoleSessionReporterTests
{
    [Fact]
    public void WriteSessionComplete_FullSuccessSession_ContainsUrlMethodAndSucceededCount()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = new StressTestOptions(
            new Uri("https://example.com/api"),
            "payload.json",
            HttpMethod.Post,
            2,
            TimeSpan.FromSeconds(1),
            1);
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null),
            new RequestOutcome(1, 2, true, false, 200, TimeSpan.FromMilliseconds(60), null)
        };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionStart(options);
        reporter.WriteSessionComplete(report);

        var output = writer.ToString();
        Assert.Contains("https://example.com/api", output, StringComparison.Ordinal);
        Assert.Contains("POST", output, StringComparison.Ordinal);
        Assert.Contains("Succeeded: 2", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_FailuresPresent_ContainsFailedCount()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions();
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, false, false, 500, TimeSpan.FromMilliseconds(40), "error")
        };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionComplete(report);

        Assert.Contains("Failed:    1", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_CancelledSession_ContainsCancelledIndicator()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions();
        var report = new SessionReport(options, [], true);

        reporter.WriteSessionComplete(report);

        Assert.Contains("Cancelled", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_NoSuccessfulLatencies_PrintsNotApplicable()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions();
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, false, false, 500, TimeSpan.FromMilliseconds(40), "error")
        };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionComplete(report);

        Assert.Contains("Latency:   N/A", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_AuthConfigured_ShowsConfiguredIndicator()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Auth = "Bearer secret-token" };

        reporter.WriteSessionStart(options);

        var output = writer.ToString();
        Assert.Contains("Auth:     configured", output, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_AuthOmitted_DoesNotShowAuthLine()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);

        reporter.WriteSessionStart(CreateOptions());

        Assert.DoesNotContain("Auth:", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_PrintsIntervalBetweenStarts()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = new StressTestOptions(
            new Uri("https://example.com/api"),
            "payload.json",
            HttpMethod.Post,
            150,
            TimeSpan.FromSeconds(15),
            1);

        reporter.WriteSessionStart(options);

        var output = writer.ToString();
        Assert.Contains("150 requests/cycle, 15s between starts", output, StringComparison.Ordinal);
        Assert.DoesNotContain("150 requests / 15s", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_SubSecondInterval_PrintsMilliseconds()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Interval = TimeSpan.FromMilliseconds(500) };

        reporter.WriteSessionStart(options);

        Assert.Contains("500ms between starts", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_FractionalSecondInterval_PrintsMilliseconds()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Interval = TimeSpan.FromMilliseconds(1500) };

        reporter.WriteSessionStart(options);

        Assert.Contains("1500ms between starts", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_PrintsRequestTimeout()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { RequestTimeout = TimeSpan.FromSeconds(30) };

        reporter.WriteSessionStart(options);

        Assert.Contains("Timeout:  30s", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_PrintsDefaultRequestTimeout()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);

        reporter.WriteSessionStart(CreateOptions());

        Assert.Contains("Timeout:  100s", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_PositiveCycleInterval_PrintsCycleGap()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { CycleInterval = TimeSpan.FromSeconds(30) };

        reporter.WriteSessionStart(options);

        Assert.Contains("Cycle gap: 30s", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_ZeroCycleInterval_OmitsCycleGap()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);

        reporter.WriteSessionStart(CreateOptions());

        Assert.DoesNotContain("Cycle gap:", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_SubSecondCycleInterval_PrintsMilliseconds()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { CycleInterval = TimeSpan.FromMilliseconds(1500) };

        reporter.WriteSessionStart(options);

        Assert.Contains("Cycle gap: 1500ms", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_SubSecondRequestTimeout_PrintsMilliseconds()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { RequestTimeout = TimeSpan.FromMilliseconds(500) };

        reporter.WriteSessionStart(options);

        Assert.Contains("Timeout:  500ms", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCycleSummary_AllFailures_PrintsZeroAverage()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, false, false, 500, TimeSpan.FromMilliseconds(40), "error"),
            new RequestOutcome(1, 2, false, false, 500, TimeSpan.FromMilliseconds(60), "error")
        };

        reporter.WriteCycleSummary(1, 1, outcomes);

        Assert.Contains("Cycle 1/1  OK 0  Fail 2  Avg 0ms", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCycleSummary_CancelledExcludedFromFailCount()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null),
            new RequestOutcome(1, 2, false, false, 500, TimeSpan.FromMilliseconds(40), "error"),
            new RequestOutcome(1, 3, false, true, null, TimeSpan.Zero, "cancelled")
        };

        reporter.WriteCycleSummary(1, 2, outcomes);

        Assert.Contains("Cycle 1/2  OK 1  Fail 1  Avg 40ms", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCycleSummary_MixedSuccesses_ComputesAverage()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null),
            new RequestOutcome(1, 2, true, false, 200, TimeSpan.FromMilliseconds(60), null)
        };

        reporter.WriteCycleSummary(1, 1, outcomes);

        Assert.Contains("Cycle 1/1  OK 2  Fail 0  Avg 50ms", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_Success_PrintsHeaderPayloadResponseAndStatus()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 2, true, false, 200, TimeSpan.FromMilliseconds(40), null, """{"id":123}""");

        reporter.WriteVerboseRequest(1, 3, 2, 10, """{"foo":"bar"}""", 2, 30, 2, 4, outcome);

        var output = writer.ToString();
        Assert.Contains("(2/30) Request 2/10 (cycle 1/3) payload 2/4", output, StringComparison.Ordinal);
        Assert.Contains("""{"foo":"bar"}""", output, StringComparison.Ordinal);
        Assert.Contains("""{"id":123}""", output, StringComparison.Ordinal);
        Assert.Contains("OK: HTTP 200 40ms", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_GetRequest_OmitsRequestBodyLine()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(1, 1, 1, 1, null, 1, 1, 1, 1, outcome);

        var output = writer.ToString();
        Assert.DoesNotContain("foo", output, StringComparison.Ordinal);
        Assert.Contains("OK: HTTP 200 40ms", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_HttpFailure_PrintsBodiesAndError()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(
            1,
            1,
            false,
            false,
            503,
            TimeSpan.FromMilliseconds(40),
            "HTTP 503 Service Unavailable — detail: over capacity",
            """{"detail":"over capacity"}""");

        reporter.WriteVerboseRequest(1, 6, 47, 100, """{"orderId":47}""", 47, 600, 3, 8, outcome);

        var output = writer.ToString();
        Assert.Contains("(47/600) Request 47/100 (cycle 1/6) payload 3/8", output, StringComparison.Ordinal);
        Assert.Contains("""{"orderId":47}""", output, StringComparison.Ordinal);
        Assert.Contains("""{"detail":"over capacity"}""", output, StringComparison.Ordinal);
        Assert.Contains("Fail: HTTP 503 Service Unavailable — detail: over capacity", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_FailureWithNullErrorMessage_OmitsErrorLine()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, false, false, null, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", 1, 1, 1, 1, outcome);

        Assert.DoesNotContain("Fail:", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_NetworkFailure_PrintsExceptionMessage()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, false, false, null, TimeSpan.FromMilliseconds(40), "Connection refused");

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", 1, 1, 1, 1, outcome);

        Assert.Contains("Fail: Connection refused", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_CancelledRequest_PrintsErrorReason()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, false, true, null, TimeSpan.FromMilliseconds(40), "Request was cancelled.");

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", 1, 1, 1, 1, outcome);

        Assert.Contains("Fail: Request was cancelled.", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_Success_DoesNotEmitAnsiColorCodesInTests()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", 1, 1, 1, 1, outcome);

        Assert.DoesNotContain("\x1b[", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_EmptyResponseBody_OmitsResponseLine()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null, null);

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", 1, 1, 1, 1, outcome);

        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void WriteVerboseRequest_AppendsBlankLineAfterRequest()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", 1, 1, 1, 1, outcome);

        Assert.EndsWith($"{Environment.NewLine}{Environment.NewLine}", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_VerboseFailures_PrintsVerboseMode()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Verbose = VerboseMode.Failures };

        reporter.WriteSessionStart(options);

        Assert.Contains("Verbose:  failures", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_VerboseOff_OmitsVerboseLine()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);

        reporter.WriteSessionStart(CreateOptions());

        Assert.DoesNotContain("Verbose:", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_VerboseOff_NoFailureDigest()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Verbose = VerboseMode.Off, RequestsPerInterval = 10, Cycles = 60 };
        var outcomes = new[]
        {
            new RequestOutcome(1, 1, false, false, 503, TimeSpan.FromMilliseconds(120), "fail", PayloadIndex: 3, PayloadCount: 8)
        };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionComplete(report);

        Assert.DoesNotContain("Failures (", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_VerboseFailures_WithFailures_PrintsDigest()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Verbose = VerboseMode.Failures, RequestsPerInterval = 100, Cycles = 6 };
        var outcomes = new[]
        {
            new RequestOutcome(1, 12, false, false, 503, TimeSpan.FromMilliseconds(120), "fail", PayloadIndex: 3, PayloadCount: 8),
            new RequestOutcome(1, 47, false, false, null, TimeSpan.FromMilliseconds(80), "Request timed out.", PayloadIndex: 1, PayloadCount: 8)
        };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionComplete(report);

        var output = writer.ToString();
        Assert.Contains("Failures (2):", output, StringComparison.Ordinal);
        Assert.Contains("(12/600) HTTP 503 payload 3/8 120ms", output, StringComparison.Ordinal);
        Assert.Contains("(47/600) timeout payload 1/8 80ms", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_VerboseFull_AllSuccess_NoDigest()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Verbose = VerboseMode.Full };
        var outcomes = new[] { new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null) };
        var report = new SessionReport(options, outcomes, false);

        reporter.WriteSessionComplete(report);

        Assert.DoesNotContain("Failures (", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionComplete_VerboseFailures_Cancelled_IncludesInDigest()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Verbose = VerboseMode.Failures };
        var outcomes = new[] { new RequestOutcome(1, 1, false, true, null, TimeSpan.Zero, "cancelled") };
        var report = new SessionReport(options, outcomes, true);

        reporter.WriteSessionComplete(report);

        Assert.Contains("(1/1) cancelled payload 1/1", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_FixedRate_PrintsLoadMode()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Load = LoadMode.FixedRate };

        reporter.WriteSessionStart(options);

        Assert.Contains("Load:     fixed-rate", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_BatchLoad_PrintsBatchSizeAndWaveInterval()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Load = LoadMode.Batch, Batch = 20, RequestsPerInterval = 100, Interval = TimeSpan.FromSeconds(1) };

        reporter.WriteSessionStart(options);

        var output = writer.ToString();
        Assert.Contains("100 requests/cycle, batch 20, 1s between wave starts", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSessionStart_BatchLoad_PrintsLoadMode()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var options = CreateOptions() with { Load = LoadMode.Batch, Batch = 5 };

        reporter.WriteSessionStart(options);

        Assert.Contains("Load:     batch", writer.ToString(), StringComparison.Ordinal);
    }

    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, 1, TimeSpan.FromSeconds(1), 1);
}
