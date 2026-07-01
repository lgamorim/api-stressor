namespace Stressor.Core.Tests;

using NSubstitute;

public class StressTestRunnerTests
{
    private readonly IJsonPayloadReader payloadReader = Substitute.For<IJsonPayloadReader>();
    private readonly IHttpStressTestClient httpClient = Substitute.For<IHttpStressTestClient>();
    private readonly IConsoleSessionReporter reporter = Substitute.For<IConsoleSessionReporter>();
    private readonly TestDelayProvider delayProvider = new();

    [Fact]
    public async Task RunAsync_OneCycleOneRequest_SendsSingleRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 1);

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(1, report.TotalRequests);
        await httpClient.Received(1).SendAsync(
            options,
            "{}",
            1,
            1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_TwoCyclesThreeRequests_SendsSixRequests()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 2, intervalMs: 3000);

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(6, report.TotalRequests);
        await httpClient.Received(6).SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_RatePacing_InvokesExpectedDelays()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 1, intervalMs: 3000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, delayProvider.Delays.Count);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(3000), delayProvider.Delays[0]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(3000), delayProvider.Delays[1]);
    }

    [Fact]
    public async Task RunAsync_MultipleCycles_WaitsIntervalBetweenConsecutiveRequests()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, intervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(delayProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), delayProvider.Delays[0]);
    }

    [Fact]
    public async Task RunAsync_SlowRequest_SkipsWaitWhenLatencyExceedsInterval()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromSeconds(2), null)));

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, true, false, 200, TimeSpan.Zero, null)));

        var runner = CreateRunner();
        var options = CreateOptions(requests: 2, cycles: 1, intervalMs: 1000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Empty(delayProvider.Delays);
    }

    [Fact]
    public async Task RunAsync_FirstRequestInSession_NeverDelaysBeforeSend()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 1, intervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Empty(delayProvider.Delays);
    }

    [Fact]
    public async Task RunAsync_CancelledBeforeFirstRequest_ReturnsEmptyReport()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var report = await runner.RunAsync(CreateOptions(), cts.Token);

        Assert.Empty(report.Outcomes);
        Assert.True(report.WasCancelled);
        await httpClient.DidNotReceive().SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CancelledMidCycle_StopsAfterInFlightRequest()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)));

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, true, null, TimeSpan.Zero, "cancelled")));

        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 1);

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.True(report.WasCancelled);
    }

    [Fact]
    public async Task RunAsync_MixedOutcomes_IncludesAllInReport()
    {
        ConfigureSuccessfulRequest();
        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, false, 500, TimeSpan.Zero, "fail")));

        var runner = CreateRunner();
        var report = await runner.RunAsync(CreateOptions(requests: 2, cycles: 1), TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.Equal(1, report.SucceededCount);
        Assert.Equal(1, report.FailedCount);
    }

    [Fact]
    public async Task RunAsync_ClientThrowsOnOneRequest_ContinuesRemainingRequests()
    {
        ConfigureSuccessfulRequest();
        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, false, false, null, TimeSpan.Zero, "boom")));

        var runner = CreateRunner();
        var report = await runner.RunAsync(CreateOptions(requests: 2, cycles: 1), TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.SucceededCount);
    }

    [Fact]
    public async Task RunAsync_SinglePayloadList_SendsSamePayloadEveryRequest()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "same" });
        ConfigureSuccessfulRequestForAnyPayload();

        var runner = CreateRunner();
        await runner.RunAsync(CreateOptions(requests: 3, cycles: 2), TestCancellation.Token);

        await httpClient.Received(6).SendAsync(
            Arg.Any<StressTestOptions>(),
            "same",
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_MultiplePayloads_RotatesAndWrapsWithinCycle()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 5, cycles: 1), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c", "a", "b"]);
    }

    [Fact]
    public async Task RunAsync_MultiplePayloads_ExactCountNoWrap()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c"]);
    }

    [Fact]
    public async Task RunAsync_MultiplePayloads_PartialCountNoWrap()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 1), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b"]);
    }

    [Fact]
    public async Task RunAsync_MultiplePayloads_OneRequestUsesFirstOnly()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 1, cycles: 1), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a"]);
    }

    [Fact]
    public async Task RunAsync_MultiplePayloads_ResetsAtStartOfNextCycle()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 4, cycles: 2), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c", "a", "a", "b", "c", "a"]);
    }

    [Fact]
    public async Task RunAsync_MultiplePayloads_FailureContinuesRotation()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, false, 500, TimeSpan.Zero, "fail")));

        var runner = CreateRunner();
        var report = await runner.RunAsync(CreateOptions(requests: 3, cycles: 1), TestCancellation.Token);

        Assert.Equal(3, report.TotalRequests);
        await AssertPayloadSequenceAsync(["a", "b", "c"]);
    }

    [Fact]
    public async Task RunAsync_MultiplePayloads_CancelledMidCycle_StopsRotation()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)));

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, true, null, TimeSpan.Zero, "cancelled")));

        var runner = CreateRunner();
        var report = await runner.RunAsync(CreateOptions(requests: 3, cycles: 1), TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.True(report.WasCancelled);
        await AssertPayloadSequenceAsync(["a", "b"]);
    }

    [Fact]
    public async Task RunAsync_VerboseFalse_DoesNotCallWriteVerboseRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1), TestCancellation.Token);

        reporter.DidNotReceive().WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<LoadMode>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_VerboseTrue_CallsWriteVerboseRequestPerRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1, verbose: true), TestCancellation.Token);

        reporter.Received(3).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<LoadMode>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_VerboseTrue_MultipleCycles_CallsForEveryRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 2, verbose: true), TestCancellation.Token);

        reporter.Received(6).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<LoadMode>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_VerboseTrue_PassesCorrectPositionAndPayload()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 2, verbose: true), TestCancellation.Token);

        reporter.Received(1).WriteVerboseRequest(
            2,
            2,
            2,
            3,
            "b",
            false,
            LoadMode.GentlePacing,
            5,
            6,
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_VerboseTrue_FailedRequest_PassesOutcomeWithError()
    {
        ConfigureSuccessfulRequest();
        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, false, false, 500, TimeSpan.Zero, "fail")));

        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 1, cycles: 1, verbose: true), TestCancellation.Token);

        reporter.Received(1).WriteVerboseRequest(
            1,
            1,
            1,
            1,
            "{}",
            false,
            LoadMode.GentlePacing,
            1,
            1,
            Arg.Is<RequestOutcome>(o => !o.IsSuccess && o.ErrorMessage == "fail"));
    }

    [Fact]
    public async Task RunAsync_PrettyPrintWithoutVerbose_CallsWriteVerboseRequestWithPrettyPrint()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 1, prettyPrint: true), TestCancellation.Token);

        reporter.Received(2).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            true,
            Arg.Any<LoadMode>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_PrettyPrintTrue_PassesPrettyPrintFlag()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 1, cycles: 1, verbose: true, prettyPrint: true), TestCancellation.Token);

        reporter.Received(1).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            true,
            Arg.Any<LoadMode>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_VerboseTrue_CancelledBeforeFirstRequest_NeverCallsWriteVerboseRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await runner.RunAsync(CreateOptions(verbose: true), cts.Token);

        reporter.DidNotReceive().WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<LoadMode>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_VerboseTrue_CancelledMidCycle_CallsOnlyForCompletedRequests()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)));

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, true, null, TimeSpan.Zero, "cancelled")));

        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1, verbose: true), TestCancellation.Token);

        reporter.Received(2).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<LoadMode>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_FixedRate_SlowRequest_StillDelaysBetweenStarts()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var requestNumber = call.ArgAt<int>(3);
                var latency = requestNumber == 1 ? TimeSpan.FromSeconds(2) : TimeSpan.Zero;
                return Task.FromResult(new RequestOutcome(1, requestNumber, true, false, 200, latency, null));
            });

        var runner = CreateRunner();
        var options = CreateOptions(requests: 2, cycles: 1, intervalMs: 1000, load: LoadMode.FixedRate);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(delayProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), delayProvider.Delays[0]);
    }

    [Fact]
    public async Task RunAsync_FixedRate_OverlappingSend_StartsSecondBeforeFirstCompletes()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        var releaseFirst = new TaskCompletionSource<RequestOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(releaseFirst.Task);

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                secondInvoked.TrySetResult();
                return Task.FromResult(new RequestOutcome(1, 2, true, false, 200, TimeSpan.Zero, null));
            });

        var runner = CreateRunner();
        var options = CreateOptions(requests: 2, cycles: 1, intervalMs: 1000, load: LoadMode.FixedRate);

        var runTask = runner.RunAsync(options, TestCancellation.Token);

        await secondInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5), TestCancellation.Token);
        Assert.False(releaseFirst.Task.IsCompleted);

        releaseFirst.SetResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromSeconds(2), null));
        await runTask;
    }

    [Fact]
    public async Task RunAsync_FixedRate_MultipleCycles_SchedulesAcrossCycleBoundary()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, intervalMs: 5000, load: LoadMode.FixedRate);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(delayProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), delayProvider.Delays[0]);
    }

    [Fact]
    public async Task RunAsync_FixedRate_CancelledBeforeFirstRequest_ReturnsEmptyReport()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var report = await runner.RunAsync(CreateOptions(load: LoadMode.FixedRate), cts.Token);

        Assert.Empty(report.Outcomes);
        Assert.True(report.WasCancelled);
        await httpClient.DidNotReceive().SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_FixedRate_CancelledMidSchedule_StopsSchedulingRemaining()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation.Token);
        var cancellingDelayProvider = new CancellingAfterFirstDelayProvider(cts);

        ConfigureSuccessfulRequestForAnyPayload();

        var runner = new StressTestRunner(payloadReader, httpClient, reporter, cancellingDelayProvider);
        var options = CreateOptions(requests: 3, cycles: 1, load: LoadMode.FixedRate);

        var report = await runner.RunAsync(options, cts.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.True(report.WasCancelled);
        await httpClient.Received(2).SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_FixedRate_MultiplePayloads_RotatesWithinCycle()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();
        var options = CreateOptions(requests: 4, cycles: 1, load: LoadMode.FixedRate);

        await runner.RunAsync(options, TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c", "a"]);
    }

    [Fact]
    public async Task RunAsync_FixedRate_MixedOutcomes_IncludesAllInReport()
    {
        ConfigureSuccessfulRequest();
        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, false, 500, TimeSpan.Zero, "fail")));

        var runner = CreateRunner();
        var report = await runner.RunAsync(CreateOptions(requests: 2, cycles: 1, load: LoadMode.FixedRate), TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.Equal(1, report.SucceededCount);
        Assert.Equal(1, report.FailedCount);
    }

    [Fact]
    public async Task RunAsync_FixedRate_ClientThrowsOnOneRequest_ContinuesRemaining()
    {
        ConfigureSuccessfulRequest();
        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, false, false, null, TimeSpan.Zero, "boom")));

        var runner = CreateRunner();
        var report = await runner.RunAsync(CreateOptions(requests: 2, cycles: 1, load: LoadMode.FixedRate), TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.SucceededCount);
    }

    [Fact]
    public async Task RunAsync_FixedRate_Verbose_PassesSessionIndexToReporter()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 2, verbose: true, load: LoadMode.FixedRate);

        await runner.RunAsync(options, TestCancellation.Token);

        reporter.Received(1).WriteVerboseRequest(
            2,
            2,
            2,
            3,
            Arg.Any<string>(),
            false,
            LoadMode.FixedRate,
            5,
            6,
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_FixedRate_PrettyPrintOnly_StillPassesSessionIndex()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 2, cycles: 1, prettyPrint: true, load: LoadMode.FixedRate);

        await runner.RunAsync(options, TestCancellation.Token);

        reporter.Received(1).WriteVerboseRequest(
            1,
            1,
            1,
            2,
            Arg.Any<string>(),
            true,
            LoadMode.FixedRate,
            1,
            2,
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task RunAsync_FixedRate_StillWritesCycleSummary()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 2, load: LoadMode.FixedRate), TestCancellation.Token);

        reporter.Received(2).WriteCycleSummary(
            Arg.Any<int>(),
            2,
            Arg.Any<IReadOnlyList<RequestOutcome>>());
    }

    [Fact]
    public async Task RunAsync_VerboseTrue_StillWritesCycleSummary()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 2, verbose: true), TestCancellation.Token);

        reporter.Received(2).WriteCycleSummary(
            Arg.Any<int>(),
            2,
            Arg.Any<IReadOnlyList<RequestOutcome>>());
    }

    [Fact]
    public async Task RunAsync_InvalidOptions_ThrowsArgumentException()
    {
        var runner = CreateRunner();
        var options = CreateOptions(cycles: 0);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(options, TestCancellation.Token));

        Assert.Contains("Cycles", exception.Message, StringComparison.OrdinalIgnoreCase);
        await payloadReader.DidNotReceive().ReadAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_PayloadFileNotFound_PropagatesException()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<string>>>(_ => throw new FileNotFoundException("Payload file not found: missing.json", "missing.json"));

        var runner = CreateRunner();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => runner.RunAsync(CreateOptions(), TestCancellation.Token));
    }

    [Fact]
    public async Task RunAsync_InvalidPayload_PropagatesJsonPayloadValidationException()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<string>>>(_ => throw new JsonPayloadValidationException("Payload file is empty or contains only whitespace."));

        var runner = CreateRunner();

        await Assert.ThrowsAsync<JsonPayloadValidationException>(
            () => runner.RunAsync(CreateOptions(), TestCancellation.Token));
    }

    private void ConfigureSuccessfulRequest()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        ConfigureSuccessfulRequestForAnyPayload();
    }

    private void ConfigureSuccessfulRequestForAnyPayload()
    {
        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var requestNumber = call.ArgAt<int>(3);
                var cycleNumber = call.ArgAt<int>(2);
                return Task.FromResult(new RequestOutcome(cycleNumber, requestNumber, true, false, 200, TimeSpan.Zero, null));
            });
    }

    private void ConfigureMultiplePayloads(string[] payloads)
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(payloads);

        ConfigureSuccessfulRequestForAnyPayload();
    }

    private async Task AssertPayloadSequenceAsync(string[] expectedPayloads)
    {
        var receivedCalls = httpClient.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IHttpStressTestClient.SendAsync))
            .Select(call => call.GetArguments()[1] as string)
            .ToList();

        Assert.Equal(expectedPayloads.Length, receivedCalls.Count);
        for (var i = 0; i < expectedPayloads.Length; i++)
        {
            Assert.Equal(expectedPayloads[i], receivedCalls[i]);
        }

        await Task.CompletedTask;
    }

    private StressTestRunner CreateRunner() =>
        new(payloadReader, httpClient, reporter, delayProvider);

    private static StressTestOptions CreateOptions(int requests = 1, int cycles = 1, int intervalMs = 1000, bool verbose = false, bool prettyPrint = false, LoadMode load = LoadMode.GentlePacing) =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, requests, TimeSpan.FromMilliseconds(intervalMs), cycles, Verbose: verbose, PrettyPrint: prettyPrint, Load: load);

    private static void AssertApproximateDelay(TimeSpan expected, TimeSpan actual)
    {
        var difference = Math.Abs((expected - actual).TotalMilliseconds);
        Assert.True(difference < 50, $"Expected delay near {expected}, but was {actual}.");
    }

    private sealed class CancellingAfterFirstDelayProvider : IDelayProvider
    {
        private readonly CancellationTokenSource cancellationTokenSource;

        public CancellingAfterFirstDelayProvider(CancellationTokenSource cancellationTokenSource)
        {
            this.cancellationTokenSource = cancellationTokenSource;
        }

        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            Delays.Add(delay);

            if (Delays.Count == 1)
            {
                cancellationTokenSource.Cancel();
            }

            return Task.CompletedTask;
        }
    }
}
