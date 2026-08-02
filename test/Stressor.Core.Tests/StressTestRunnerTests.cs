namespace Stressor.Core.Tests;

using NSubstitute;

public class StressTestRunnerTests
{
    private readonly IJsonPayloadReader _payloadReader = Substitute.For<IJsonPayloadReader>();
    private readonly IHttpStressTestClient _httpClient = Substitute.For<IHttpStressTestClient>();
    private readonly IConsoleSessionReporter _reporter = Substitute.For<IConsoleSessionReporter>();
    private readonly RecordingTimeProvider _timeProvider = new();

    [Fact]
    public async Task Should_SendSingleRequest_When_OneCycleOneRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 1);

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(1, report.TotalRequests);
        await _httpClient.Received(1).SendAsync(
            options,
            "{}",
            1,
            1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SendSixRequests_When_TwoCyclesThreeRequests()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 2, intervalMs: 3000);

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(6, report.TotalRequests);
        await _httpClient.Received(6).SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_InvokeExpectedDelay_When_RatePacing()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 1, intervalMs: 3000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, _timeProvider.Delays.Count);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(3000), _timeProvider.Delays[0]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(3000), _timeProvider.Delays[1]);
    }

    [Fact]
    public async Task Should_WaitIntervalBetweenConsecutiveRequests_When_MultipleCycles()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, intervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_SkipWaitWhenLatencyExceedInterval_When_SlowRequest()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromSeconds(2), null)));

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, true, false, 200, TimeSpan.Zero, null)));

        var runner = CreateRunner();
        var options = CreateOptions(requests: 2, cycles: 1, intervalMs: 1000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Empty(_timeProvider.Delays);
    }

    [Fact]
    public async Task Should_NeverDelayBeforeSend_When_FirstRequestInSession()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 1, intervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Empty(_timeProvider.Delays);
    }

    [Fact]
    public async Task Should_ReturnEmptyReport_When_CancelledBeforeFirstRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var report = await runner.RunAsync(CreateOptions(), cts.Token);

        Assert.Empty(report.Outcomes);
        Assert.True(report.WasCancelled);
        await _httpClient.DidNotReceive().SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_StopAfterInFlightRequest_When_CancelledMidCycle()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)));

        _httpClient.SendAsync(
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
    public async Task Should_IncludeAllInReport_When_MixedOutcomes()
    {
        ConfigureSuccessfulRequest();
        _httpClient.SendAsync(
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
    public async Task Should_ContinueRemainingRequests_When_ClientThrowOnOneRequest()
    {
        ConfigureSuccessfulRequest();
        _httpClient.SendAsync(
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
    public async Task Should_SendSamePayloadEveryRequest_When_SinglePayloadList()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "same" });
        ConfigureSuccessfulRequestForAnyPayload();

        var runner = CreateRunner();
        await runner.RunAsync(CreateOptions(requests: 3, cycles: 2), TestCancellation.Token);

        await _httpClient.Received(6).SendAsync(
            Arg.Any<StressTestOptions>(),
            "same",
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RotateAndWrapsWithinCycle_When_MultiplePayloads()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 5, cycles: 1), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c", "a", "b"]);
    }

    [Fact]
    public async Task Should_ExactCountNoWrap_When_MultiplePayloads()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c"]);
    }

    [Fact]
    public async Task Should_PartialCountNoWrap_When_MultiplePayloads()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 1), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b"]);
    }

    [Fact]
    public async Task Should_OneRequestUseFirstOnly_When_MultiplePayloads()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 1, cycles: 1), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a"]);
    }

    [Fact]
    public async Task Should_ResetAtStartOfNextCycle_When_MultiplePayloads()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 4, cycles: 2), TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c", "a", "a", "b", "c", "a"]);
    }

    [Fact]
    public async Task Should_FailureContinueRotation_When_MultiplePayloads()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        _httpClient.SendAsync(
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
    public async Task Should_StopRotation_When_MultiplePayloads_CancelledMidCycle()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)));

        _httpClient.SendAsync(
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
    public async Task Should_NotCallWriteVerboseRequest_When_VerboseOff()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1), TestCancellation.Token);

        _reporter.DidNotReceive().WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task Should_CallWriteVerboseRequestPerRequest_When_VerboseFull()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1, verbose: VerboseMode.Full), TestCancellation.Token);

        _reporter.Received(3).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task Should_CallForEveryRequest_When_VerboseFull_MultipleCycles()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 2, verbose: VerboseMode.Full), TestCancellation.Token);

        _reporter.Received(6).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task Should_PassCorrectPositionPayloadAndIndex_When_VerboseFull()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 2, verbose: VerboseMode.Full), TestCancellation.Token);

        _reporter.Received(1).WriteVerboseRequest(
            2,
            2,
            2,
            3,
            "b",
            5,
            6,
            2,
            3,
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task Should_NeverCallWriteVerboseRequest_When_VerboseFailures_AllSuccess()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1, verbose: VerboseMode.Failures), TestCancellation.Token);

        _reporter.DidNotReceive().WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task Should_CallOnlyForFailures_When_VerboseFailures_Mixed()
    {
        ConfigureSuccessfulRequest();
        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, false, 500, TimeSpan.Zero, "fail")));

        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1, verbose: VerboseMode.Failures), TestCancellation.Token);

        _reporter.Received(1).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Is<int>(n => n == 2),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Is<RequestOutcome>(o => !o.IsSuccess));
    }

    [Fact]
    public async Task Should_PassOutcomeWithError_When_VerboseFull_FailedRequest()
    {
        ConfigureSuccessfulRequest();
        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, false, false, 500, TimeSpan.Zero, "fail")));

        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 1, cycles: 1, verbose: VerboseMode.Full), TestCancellation.Token);

        _reporter.Received(1).WriteVerboseRequest(
            1,
            1,
            1,
            1,
            "{}",
            1,
            1,
            1,
            1,
            Arg.Is<RequestOutcome>(o => !o.IsSuccess && o.ErrorMessage == "fail"));
    }

    [Fact]
    public async Task Should_NeverCallWriteVerboseRequest_When_VerboseFull_CancelledBeforeFirstRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await runner.RunAsync(CreateOptions(verbose: VerboseMode.Full), cts.Token);

        _reporter.DidNotReceive().WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task Should_CallOnlyForCompletedRequests_When_VerboseFull_CancelledMidCycle()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)));

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, true, null, TimeSpan.Zero, "cancelled")));

        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 3, cycles: 1, verbose: VerboseMode.Full), TestCancellation.Token);

        _reporter.Received(2).WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Theory]
    [InlineData(VerboseMode.Off, true, false)]
    [InlineData(VerboseMode.Full, true, true)]
    [InlineData(VerboseMode.Failures, true, false)]
    [InlineData(VerboseMode.Failures, false, true)]
    public void Should_ReturnExpectedResult_When_ShouldReportVerbose(VerboseMode mode, bool isSuccess, bool expected)
    {
        var outcome = new RequestOutcome(1, 1, isSuccess, false, isSuccess ? 200 : 500, TimeSpan.Zero, isSuccess ? null : "fail");

        Assert.Equal(expected, StressTestRunner.ShouldReportVerbose(mode, outcome));
    }

    [Fact]
    public async Task Should_StillDelayBetweenStart_When_FixedRate_SlowRequest()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        _httpClient.SendAsync(
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

        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_StartSecondBeforeFirstComplete_When_FixedRate_OverlappingSend()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        var releaseFirst = new TaskCompletionSource<RequestOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(releaseFirst.Task);

        _httpClient.SendAsync(
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
    public async Task Should_ScheduleAcrossCycleBoundary_When_FixedRate_MultipleCycles()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, intervalMs: 5000, load: LoadMode.FixedRate);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_ReturnEmptyReport_When_FixedRate_CancelledBeforeFirstRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var report = await runner.RunAsync(CreateOptions(load: LoadMode.FixedRate), cts.Token);

        Assert.Empty(report.Outcomes);
        Assert.True(report.WasCancelled);
        await _httpClient.DidNotReceive().SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_StopSchedulingRemaining_When_FixedRate_CancelledMidSchedule()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation.Token);
        var cancellingTimeProvider = new CancellingAfterFirstDelayTimeProvider(cts);

        ConfigureSuccessfulRequestForAnyPayload();

        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, cancellingTimeProvider);
        var options = CreateOptions(requests: 3, cycles: 1, load: LoadMode.FixedRate);

        var report = await runner.RunAsync(options, cts.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.True(report.WasCancelled);
        await _httpClient.Received(2).SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RotateWithinCycle_When_FixedRate_MultiplePayloads()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();
        var options = CreateOptions(requests: 4, cycles: 1, load: LoadMode.FixedRate);

        await runner.RunAsync(options, TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c", "a"]);
    }

    [Fact]
    public async Task Should_IncludeAllInReport_When_FixedRate_MixedOutcomes()
    {
        ConfigureSuccessfulRequest();
        _httpClient.SendAsync(
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
    public async Task Should_ContinueRemaining_When_FixedRate_ClientThrowOnOneRequest()
    {
        ConfigureSuccessfulRequest();
        _httpClient.SendAsync(
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
    public async Task Should_PassSessionIndexToReporter_When_FixedRate_VerboseFull()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 2, verbose: VerboseMode.Full, load: LoadMode.FixedRate);

        await runner.RunAsync(options, TestCancellation.Token);

        _reporter.Received(1).WriteVerboseRequest(
            2,
            2,
            2,
            3,
            Arg.Any<string?>(),
            5,
            6,
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task Should_StillWriteCycleSummary_When_FixedRate()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 2, load: LoadMode.FixedRate), TestCancellation.Token);

        _reporter.Received(2).WriteCycleSummary(
            Arg.Any<int>(),
            2,
            Arg.Any<IReadOnlyList<RequestOutcome>>());
    }

    [Fact]
    public async Task Should_StillWriteCycleSummary_When_VerboseTrue()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 2, verbose: VerboseMode.Full), TestCancellation.Token);

        _reporter.Received(2).WriteCycleSummary(
            Arg.Any<int>(),
            2,
            Arg.Any<IReadOnlyList<RequestOutcome>>());
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_InvalidOptions()
    {
        var runner = CreateRunner();
        var options = CreateOptions(cycles: 0);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(options, TestCancellation.Token));

        Assert.Contains("Cycles", exception.Message, StringComparison.OrdinalIgnoreCase);
        await _payloadReader.DidNotReceive().ReadAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_PropagateException_When_PayloadFileNotFound()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<string>>>(_ => throw new FileNotFoundException("Payload file not found: missing.json", "missing.json"));

        var runner = CreateRunner();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => runner.RunAsync(CreateOptions(), TestCancellation.Token));
    }

    [Fact]
    public async Task Should_PropagateJsonPayloadValidationException_When_InvalidPayload()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<string>>>(_ => throw new JsonPayloadValidationException("Payload file is empty or contains only whitespace."));

        var runner = CreateRunner();

        await Assert.ThrowsAsync<JsonPayloadValidationException>(
            () => runner.RunAsync(CreateOptions(), TestCancellation.Token));
    }

    [Fact]
    public async Task Should_SendSingleRequest_When_Batch_OneCycleOneRequest()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 1, load: LoadMode.Batch);

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(1, report.TotalRequests);
        await _httpClient.Received(1).SendAsync(
            options,
            "{}",
            1,
            1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AllRequestsStartInParallel_When_Batch_SingleWave()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        var releaseFirst = new TaskCompletionSource<RequestOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(releaseFirst.Task);

        _httpClient.SendAsync(
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

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 3),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 3, true, false, 200, TimeSpan.Zero, null)));

        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 1, load: LoadMode.Batch, batch: 3);

        var runTask = runner.RunAsync(options, TestCancellation.Token);

        await secondInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5), TestCancellation.Token);
        Assert.False(releaseFirst.Task.IsCompleted);

        releaseFirst.SetResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromSeconds(2), null));
        await runTask;
    }

    [Fact]
    public async Task Should_SendAllRequests_When_Batch_MultipleWaves()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 5, cycles: 1, load: LoadMode.Batch, batch: 2);

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(5, report.TotalRequests);
        await _httpClient.Received(5).SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_UseCorrectPayloadRotation_When_Batch_PartialFinalWave()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();
        var options = CreateOptions(requests: 7, cycles: 1, load: LoadMode.Batch, batch: 3);

        await runner.RunAsync(options, TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c", "a", "b", "c", "a"]);
    }

    [Fact]
    public async Task Should_InvokeExpectedDelay_When_Batch_IntervalBetweenWaveStart()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 6, cycles: 1, intervalMs: 3000, load: LoadMode.Batch, batch: 2);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, _timeProvider.Delays.Count);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(3000), _timeProvider.Delays[0]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(3000), _timeProvider.Delays[1]);
    }

    [Fact]
    public async Task Should_NeverDelayBeforeSend_When_Batch_FirstWaveInSession()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 1, intervalMs: 5000, load: LoadMode.Batch, batch: 3);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Empty(_timeProvider.Delays);
    }

    [Fact]
    public async Task Should_SkipWaitWhenLatencyExceedInterval_When_Batch_SlowWave()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.FromSeconds(2), null)));

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, true, false, 200, TimeSpan.Zero, null)));

        var runner = CreateRunner();
        var options = CreateOptions(requests: 2, cycles: 1, intervalMs: 1000, load: LoadMode.Batch, batch: 1);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Empty(_timeProvider.Delays);
    }

    [Fact]
    public async Task Should_WaitIntervalBetweenLastWaveAndNextCycleFirstWave_When_Batch_MultipleCycles()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, intervalMs: 5000, load: LoadMode.Batch, batch: 1);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_NoDelayBetweenWaves_When_Batch_ZeroInterval()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 6, cycles: 1, intervalMs: 0, load: LoadMode.Batch, batch: 2);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Empty(_timeProvider.Delays);
    }

    [Fact]
    public async Task Should_ReturnEmptyReport_When_Batch_CancelledBeforeFirstWave()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var report = await runner.RunAsync(CreateOptions(requests: 3, load: LoadMode.Batch, batch: 3), cts.Token);

        Assert.Empty(report.Outcomes);
        Assert.True(report.WasCancelled);
        await _httpClient.DidNotReceive().SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_StopSchedulingRemainingWaves_When_Batch_CancelledMidCycle()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation.Token);
        var cancellingTimeProvider = new CancellingAfterFirstDelayTimeProvider(cts);

        ConfigureSuccessfulRequestForAnyPayload();

        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, cancellingTimeProvider);
        var options = CreateOptions(requests: 6, cycles: 1, load: LoadMode.Batch, batch: 2);

        var report = await runner.RunAsync(options, cts.Token);

        Assert.Equal(4, report.TotalRequests);
        Assert.True(report.WasCancelled);
    }

    [Fact]
    public async Task Should_IncludeAllInReport_When_Batch_MixedOutcomes()
    {
        ConfigureSuccessfulRequest();
        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, false, 500, TimeSpan.Zero, "fail")));

        var runner = CreateRunner();
        var report = await runner.RunAsync(CreateOptions(requests: 2, cycles: 1, load: LoadMode.Batch, batch: 2), TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.Equal(1, report.SucceededCount);
        Assert.Equal(1, report.FailedCount);
    }

    [Fact]
    public async Task Should_PassSessionIndexToReporter_When_Batch_VerboseFull()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 2, verbose: VerboseMode.Full, load: LoadMode.Batch, batch: 3);

        await runner.RunAsync(options, TestCancellation.Token);

        _reporter.Received().WriteVerboseRequest(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<RequestOutcome>());
    }

    [Fact]
    public async Task Should_StillWriteCycleSummary_When_Batch()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 4, cycles: 2, load: LoadMode.Batch, batch: 2), TestCancellation.Token);

        _reporter.Received(2).WriteCycleSummary(
            Arg.Any<int>(),
            2,
            Arg.Any<IReadOnlyList<RequestOutcome>>());
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_Batch_InvalidOptions()
    {
        var runner = CreateRunner();
        var options = CreateOptions(requests: 5, load: LoadMode.Batch, batch: 10);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(options, TestCancellation.Token));

        Assert.Contains("Batch size", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_NoCycleGapDelay_When_GentlePacing_SingleCycleWithCycleInterval()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 1, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Empty(_timeProvider.Delays);
    }

    [Fact]
    public async Task Should_WaitOnceBetweenCycles_When_GentlePacing_TwoCyclesWithCycleIntervalOnly()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_WaitBetweenEachCycle_When_GentlePacing_ThreeCyclesWithCycleIntervalOnly()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 3, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, _timeProvider.Delays.Count);
        Assert.All(_timeProvider.Delays, delay => AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), delay));
    }

    [Fact]
    public async Task Should_CycleIntervalReplaceIntervalAtBoundary_When_GentlePacing()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, intervalMs: 5000, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_CycleIntervalStartNextCycleImmediately_When_GentlePacing()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 2, cycles: 2, intervalMs: 1000, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(3, _timeProvider.Delays.Count);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), _timeProvider.Delays[0]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[1]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), _timeProvider.Delays[2]);
    }

    [Fact]
    public async Task Should_StopBeforeNextCycle_When_GentlePacing_CancelledDuringCycleGap()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation.Token);
        var cancellingTimeProvider = new CancellingAfterFirstDelayTimeProvider(cts);

        ConfigureSuccessfulRequestForAnyPayload();

        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, cancellingTimeProvider);
        var options = CreateOptions(requests: 1, cycles: 2, cycleIntervalMs: 5000);

        var report = await runner.RunAsync(options, cts.Token);

        Assert.Equal(1, report.TotalRequests);
        Assert.True(report.WasCancelled);
        await _httpClient.Received(1).SendAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<string>(),
            Arg.Is<int>(cycle => cycle == 1),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_NotWaitForCycleGap_When_GentlePacing_CancelledMidCycle()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)));

        _httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Is<int>(n => n == 2),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RequestOutcome(1, 2, false, true, null, TimeSpan.Zero, "cancelled")));

        var runner = CreateRunner();
        var options = CreateOptions(requests: 3, cycles: 2, cycleIntervalMs: 5000);

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.True(report.WasCancelled);
        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_ResetAtNextCycle_When_GentlePacing_CycleIntervalWithMultiplePayloads()
    {
        ConfigureMultiplePayloads(["a", "b", "c"]);
        var runner = CreateRunner();
        var options = CreateOptions(requests: 4, cycles: 2, cycleIntervalMs: 1000);

        await runner.RunAsync(options, TestCancellation.Token);

        await AssertPayloadSequenceAsync(["a", "b", "c", "a", "a", "b", "c", "a"]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), _timeProvider.Delays[3]);
    }

    [Fact]
    public async Task Should_WaitOnceBetweenCycles_When_Batch_TwoCyclesWithCycleIntervalOnly()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, load: LoadMode.Batch, batch: 1, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_WaitBetweenCycles_When_Batch_PartialFinalWaveWithCycleInterval()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 7, cycles: 2, load: LoadMode.Batch, batch: 3, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(5, _timeProvider.Delays.Count);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[2]);
    }

    [Fact]
    public async Task Should_CycleIntervalReplaceIntervalAtBoundary_When_Batch()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, intervalMs: 5000, load: LoadMode.Batch, batch: 1, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Single(_timeProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), _timeProvider.Delays[0]);
    }

    [Fact]
    public async Task Should_StopBeforeNextCycle_When_Batch_CancelledDuringCycleGap()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation.Token);
        var cancellingTimeProvider = new CancellingAfterFirstDelayTimeProvider(cts);

        ConfigureSuccessfulRequestForAnyPayload();

        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, cancellingTimeProvider);
        var options = CreateOptions(requests: 1, cycles: 2, load: LoadMode.Batch, batch: 1, cycleIntervalMs: 5000);

        var report = await runner.RunAsync(options, cts.Token);

        Assert.Equal(1, report.TotalRequests);
        Assert.True(report.WasCancelled);
    }

    [Fact]
    public async Task Should_InsertGapInSchedule_When_FixedRate_TwoCyclesWithCycleInterval()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 2, cycles: 2, intervalMs: 1000, load: LoadMode.FixedRate, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(3, _timeProvider.Delays.Count);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), _timeProvider.Delays[0]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(6000), _timeProvider.Delays[1]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), _timeProvider.Delays[2]);
    }

    [Fact]
    public async Task Should_InsertGapBetweenEachCycle_When_FixedRate_ThreeCyclesWithCycleInterval()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 3, intervalMs: 1000, load: LoadMode.FixedRate, cycleIntervalMs: 5000);

        await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, _timeProvider.Delays.Count);
        Assert.All(_timeProvider.Delays, delay => AssertApproximateDelay(TimeSpan.FromMilliseconds(6000), delay));
    }

    [Fact]
    public async Task Should_StopBeforeNextCycle_When_FixedRate_CancelledDuringCycleGap()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation.Token);
        var cancellingTimeProvider = new CancellingAfterFirstDelayTimeProvider(cts);

        ConfigureSuccessfulRequestForAnyPayload();

        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, cancellingTimeProvider);
        var options = CreateOptions(requests: 1, cycles: 2, load: LoadMode.FixedRate, cycleIntervalMs: 5000);

        var report = await runner.RunAsync(options, cts.Token);

        Assert.Equal(2, report.TotalRequests);
        Assert.True(report.WasCancelled);
    }

    [Fact]
    public async Task Should_RouteByLoadSwitch_When_Batch()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        var report = await runner.RunAsync(CreateOptions(load: LoadMode.Batch, batch: 1), TestCancellation.Token);

        Assert.Equal(1, report.TotalRequests);
    }

    private void ConfigureSuccessfulRequest()
    {
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "{}" });

        ConfigureSuccessfulRequestForAnyPayload();
    }

    private void ConfigureSuccessfulRequestForAnyPayload()
    {
        _httpClient.SendAsync(
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
        _payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(payloads);

        ConfigureSuccessfulRequestForAnyPayload();
    }

    private async Task AssertPayloadSequenceAsync(string[] expectedPayloads)
    {
        var receivedCalls = _httpClient.ReceivedCalls()
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
        new(_payloadReader, _httpClient, _reporter, _timeProvider);

    [Fact]
    public async Task Should_RunMultipleCycles_When_DurationNotYetElapsed()
    {
        ConfigureSuccessfulRequest();
        var advancingTime = new AdvancingTimeProvider();
        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, advancingTime);
        var options = CreateOptions(requests: 1, cycles: 1, cycleIntervalMs: 1000, duration: TimeSpan.FromSeconds(5));

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(5, report.TotalRequests);
    }

    [Fact]
    public async Task Should_StopAfterCycleBoundary_When_DurationElapsed()
    {
        ConfigureSuccessfulRequest();
        var advancingTime = new AdvancingTimeProvider();
        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, advancingTime);
        var options = CreateOptions(requests: 1, cycles: 1, cycleIntervalMs: 2000, duration: TimeSpan.FromSeconds(3));

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
    }

    [Fact]
    public async Task Should_RunOneCycle_When_DurationLongerThanOneCycle()
    {
        ConfigureSuccessfulRequest();
        var advancingTime = new AdvancingTimeProvider();
        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, advancingTime);
        var options = CreateOptions(requests: 2, cycles: 1, intervalMs: 1000, cycleIntervalMs: 5000, duration: TimeSpan.FromMilliseconds(1));

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(2, report.TotalRequests);
    }

    [Fact]
    public async Task Should_WorkInDurationMode_When_FixedRate()
    {
        ConfigureSuccessfulRequest();
        var advancingTime = new AdvancingTimeProvider();
        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, advancingTime);
        var options = CreateOptions(requests: 1, cycles: 1, intervalMs: 1000, load: LoadMode.FixedRate, cycleIntervalMs: 1000, duration: TimeSpan.FromSeconds(3));

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(3, report.TotalRequests);
    }

    [Fact]
    public async Task Should_WorkInDurationMode_When_Batch()
    {
        ConfigureSuccessfulRequest();
        var advancingTime = new AdvancingTimeProvider();
        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, advancingTime);
        var options = CreateOptions(requests: 2, cycles: 1, intervalMs: 0, load: LoadMode.Batch, batch: 2, cycleIntervalMs: 1000, duration: TimeSpan.FromSeconds(3));

        var report = await runner.RunAsync(options, TestCancellation.Token);

        Assert.Equal(6, report.TotalRequests);
    }

    [Fact]
    public async Task Should_WriteProgressNotCycleSummary_When_ProgressEnabled()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 2, progress: true), TestCancellation.Token);

        _reporter.DidNotReceive().WriteCycleSummary(
            Arg.Any<int>(),
            Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<RequestOutcome>>());
        _reporter.Received(2).WriteProgress(Arg.Any<SessionProgressSnapshot>());
    }

    [Fact]
    public async Task Should_NotWriteProgress_When_VerboseFailures()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 2, progress: true, verbose: VerboseMode.Failures), TestCancellation.Token);

        _reporter.DidNotReceive().WriteProgress(Arg.Any<SessionProgressSnapshot>());
        _reporter.Received(2).WriteCycleSummary(
            Arg.Any<int>(),
            2,
            Arg.Any<IReadOnlyList<RequestOutcome>>());
    }

    [Fact]
    public async Task Should_WriteProgressWithElapsed_When_DurationMode()
    {
        ConfigureSuccessfulRequest();
        var advancingTime = new AdvancingTimeProvider();
        var runner = new StressTestRunner(_payloadReader, _httpClient, _reporter, advancingTime);
        var options = CreateOptions(requests: 1, cycles: 1, cycleIntervalMs: 1000, duration: TimeSpan.FromSeconds(5), progress: true);

        await runner.RunAsync(options, TestCancellation.Token);

        _reporter.Received().WriteProgress(Arg.Is<SessionProgressSnapshot>(snapshot =>
            snapshot.TotalDuration == TimeSpan.FromSeconds(5)
            && snapshot.Elapsed != null
            && snapshot.TotalRequests == null));
    }

    [Fact]
    public async Task Should_WriteProgressInFixedRate_When_ProgressEnabled()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(requests: 2, cycles: 2, load: LoadMode.FixedRate, progress: true), TestCancellation.Token);

        _reporter.DidNotReceive().WriteCycleSummary(
            Arg.Any<int>(),
            Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<RequestOutcome>>());
        _reporter.Received(2).WriteProgress(Arg.Any<SessionProgressSnapshot>());
    }

    private static StressTestOptions CreateOptions(int requests = 1, int cycles = 1, int intervalMs = 1000, VerboseMode verbose = VerboseMode.Off, LoadMode load = LoadMode.GentlePacing, int batch = 1, int cycleIntervalMs = 0, TimeSpan? duration = null, bool progress = false) =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, requests, TimeSpan.FromMilliseconds(intervalMs), cycles, Verbose: verbose, Load: load, Batch: batch)
        {
            CycleInterval = TimeSpan.FromMilliseconds(cycleIntervalMs),
            Duration = duration,
            Progress = progress
        };

    private static void AssertApproximateDelay(TimeSpan expected, TimeSpan actual)
    {
        var difference = Math.Abs((expected - actual).TotalMilliseconds);
        Assert.True(difference < 50, $"Expected delay near {expected}, but was {actual}.");
    }

}
