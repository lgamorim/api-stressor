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
    public void WriteVerboseRequest_Success_PrintsPositionAndPayload()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 2, true, false, 200, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(1, 3, 2, 10, """{"foo":"bar"}""", false, outcome);

        var output = writer.ToString();
        Assert.Contains("Request 2/10 (cycle 1/3)", output, StringComparison.Ordinal);
        Assert.Contains("""{"foo":"bar"}""", output, StringComparison.Ordinal);
        Assert.Contains("OK: 40ms", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Fail:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_PrettyPrintTrue_PrintsIndentedPayload()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(1, 1, 1, 1, """{"foo":"bar"}""", true, outcome);

        Assert.Contains(JsonPayloadFormatter.PrettyPrint("""{"foo":"bar"}"""), writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_Success_DoesNotEmitAnsiColorCodesInTests()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", false, outcome);

        Assert.DoesNotContain("\x1b[", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_HttpFailure_DoesNotEmitAnsiColorCodesInTests()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, false, false, 500, TimeSpan.FromMilliseconds(40), "HTTP 500 Internal Server Error");

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", false, outcome);

        Assert.DoesNotContain("\x1b[", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_HttpFailure_PrintsErrorReason()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, false, false, 500, TimeSpan.FromMilliseconds(40), "HTTP 500 Internal Server Error");

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", false, outcome);

        Assert.Contains("Fail: HTTP 500 Internal Server Error", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_NetworkFailure_PrintsExceptionMessage()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, false, false, null, TimeSpan.FromMilliseconds(40), "Connection refused");

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", false, outcome);

        Assert.Contains("Fail: Connection refused", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_CancelledRequest_PrintsErrorReason()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, false, true, null, TimeSpan.FromMilliseconds(40), "Request was cancelled.");

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", false, outcome);

        var output = writer.ToString();
        Assert.Contains("Request 1/1 (cycle 1/1)", output, StringComparison.Ordinal);
        Assert.Contains("{}", output, StringComparison.Ordinal);
        Assert.Contains("Fail: Request was cancelled.", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_SecondCycle_PrintsCorrectPosition()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(2, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(2, 2, 1, 3, "{}", false, outcome);

        Assert.Contains("Request 1/3 (cycle 2/2)", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_MultilinePayload_PrintsPrettyPrinted()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null);
        const string payload = "{\n  \"a\": 1\n}";

        reporter.WriteVerboseRequest(1, 1, 1, 1, payload, true, outcome);

        Assert.Contains(JsonPayloadFormatter.PrettyPrint(payload), writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_FailureWithNullErrorMessage_OmitsErrorLine()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var outcome = new RequestOutcome(1, 1, false, false, null, TimeSpan.FromMilliseconds(40), null);

        reporter.WriteVerboseRequest(1, 1, 1, 1, "{}", false, outcome);

        Assert.DoesNotContain("Fail:", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteVerboseRequest_AppendsBlankLineBetweenRequests()
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var reporter = new ConsoleSessionReporter(writer);
        var success = new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromMilliseconds(40), null);
        var failure = new RequestOutcome(1, 2, false, false, 500, TimeSpan.FromMilliseconds(40), "fail");

        reporter.WriteVerboseRequest(1, 1, 1, 2, "{}", false, success);
        reporter.WriteVerboseRequest(1, 1, 2, 2, """{"a":1}""", false, failure);

        var output = writer.ToString();
        Assert.Contains($"{{}}{Environment.NewLine}OK: 40ms{Environment.NewLine}{Environment.NewLine}Request 2/2", output, StringComparison.Ordinal);
        Assert.EndsWith($"Fail: fail{Environment.NewLine}{Environment.NewLine}", output, StringComparison.Ordinal);
    }

    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, 1, TimeSpan.FromSeconds(1), 1);
}
