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

        var report = await runner.RunAsync(options);

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

        var report = await runner.RunAsync(options);

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

        await runner.RunAsync(options);

        Assert.True(delayProvider.Delays.Count >= 2);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), delayProvider.Delays[0]);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(1000), delayProvider.Delays[1]);
    }

    [Fact]
    public async Task RunAsync_CycleCompletesEarly_WaitsRemainderBeforeNextCycle()
    {
        ConfigureSuccessfulRequest();
        var runner = CreateRunner();
        var options = CreateOptions(requests: 1, cycles: 2, intervalMs: 5000);

        await runner.RunAsync(options);

        Assert.Single(delayProvider.Delays);
        AssertApproximateDelay(TimeSpan.FromMilliseconds(5000), delayProvider.Delays[0]);
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
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("{}");

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

        var report = await runner.RunAsync(options);

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
        var report = await runner.RunAsync(CreateOptions(requests: 2, cycles: 1));

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
        var report = await runner.RunAsync(CreateOptions(requests: 2, cycles: 1));

        Assert.Equal(2, report.TotalRequests);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.SucceededCount);
    }

    private void ConfigureSuccessfulRequest()
    {
        payloadReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("{}");

        httpClient.SendAsync(
                Arg.Any<StressTestOptions>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var requestNumber = call.ArgAt<int>(3);
                return Task.FromResult(new RequestOutcome(1, requestNumber, true, false, 200, TimeSpan.Zero, null));
            });
    }

    private StressTestRunner CreateRunner() =>
        new(payloadReader, httpClient, reporter, delayProvider);

    private static StressTestOptions CreateOptions(int requests = 1, int cycles = 1, int intervalMs = 1000) =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, requests, TimeSpan.FromMilliseconds(intervalMs), cycles);

    private static void AssertApproximateDelay(TimeSpan expected, TimeSpan actual)
    {
        var difference = Math.Abs((expected - actual).TotalMilliseconds);
        Assert.True(difference < 50, $"Expected delay near {expected}, but was {actual}.");
    }
}
