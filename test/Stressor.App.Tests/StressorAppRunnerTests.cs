namespace Stressor.App.Tests;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Stressor.App;
using Stressor.Core;

public class StressorAppRunnerTests
{
    [Fact]
    public async Task RunAsync_AllRequiredArgsWithSuccess_ReturnsExitCodeZero()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var options = CreateOptions();
        var report = new SessionReport(options, [new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)], false);
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>()).Returns(report);

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs());

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_MissingUrl_ReturnsNonZeroExitCode()
    {
        var exitCode = await new StressorAppRunner(CreateProvider()).RunAsync(
            ["--payload", "payload.json", "--requests", "1", "--interval", "1s"],
            TestCancellation.Token);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_MissingPayload_ReturnsNonZeroExitCode()
    {
        var exitCode = await new StressorAppRunner(CreateProvider()).RunAsync(
            ["--url", "https://example.com", "--requests", "1", "--interval", "1s"],
            TestCancellation.Token);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_AllRequiredArgsWithoutCycles_ReturnsExitCodeZero()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var options = CreateOptions();
        var report = new SessionReport(options, [new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)], false);
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>()).Returns(report);

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(cycles: null));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_MethodOmitted_UsesPost()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var options = callInfo.ArgAt<StressTestOptions>(0);
                Assert.Equal(HttpMethod.Post.Method, options.Method.Method);
                return new SessionReport(options, [], false);
            });

        await ExecuteWithRunner(stressTestRunner, CreateArgs(method: null));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Method == HttpMethod.Post),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_MethodPutCaseInsensitive_BindsToPut()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(method: "put"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Method == HttpMethod.Put),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_InvalidMethod_ReturnsExitCodeOne()
    {
        var exitCode = await ExecuteWithRunner(
            Substitute.For<IStressTestRunner>(),
            CreateArgs(method: "INVALID"));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_CancelledSession_ReturnsExitCodeTwo()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var report = new SessionReport(CreateOptions(), [], true);
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>()).Returns(report);

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs());

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_FailedRequests_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var options = CreateOptions();
        var report = new SessionReport(
            options,
            [new RequestOutcome(1, 1, false, false, 500, TimeSpan.Zero, "error")],
            false);
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>()).Returns(report);

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs());

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuthOmitted_BindsNullAuth()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(auth: null));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Auth == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_AuthProvided_BindsAuthValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(auth: "Bearer secret-token"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Auth == "Bearer secret-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhitespaceAuth_ReturnsExitCodeOne()
    {
        var exitCode = await ExecuteWithRunner(
            Substitute.For<IStressTestRunner>(),
            CreateArgs(auth: "   "));

        Assert.Equal(1, exitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task RunAsync_Help_ReturnsExitCodeZeroAndPrintsUsageGuide(string helpArg)
    {
        var originalOut = Console.Out;

        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);

            var exitCode = await new StressorAppRunner(CreateProvider()).RunAsync([helpArg], TestCancellation.Token);

            Assert.Equal(0, exitCode);

            var output = writer.ToString();
            Assert.Contains("Stress tests an API endpoint.", output);
            Assert.Contains("--url", output);
            Assert.Contains("Examples:", output);
            Assert.Contains("Exit codes:", output);
            Assert.Contains("--cycles", output);
            Assert.Contains("default: 1", output, StringComparison.OrdinalIgnoreCase);

            var lines = output.Split('\n');
            var cyclesLineIndex = Array.FindIndex(lines, line => line.Contains("--cycles", StringComparison.Ordinal));
            var cycleIntervalLineIndex = Array.FindIndex(lines, line => line.Contains("--cycle-interval", StringComparison.Ordinal));
            Assert.True(cyclesLineIndex >= 0);
            Assert.True(cycleIntervalLineIndex >= 0);
            Assert.Equal(cyclesLineIndex + 1, cycleIntervalLineIndex);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_Help_DoesNotRunStressTest()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var originalOut = Console.Out;

        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);

            await new StressorAppRunner(CreateProvider(stressTestRunner)).RunAsync(["--help"], TestCancellation.Token);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_Version_ReturnsExitCodeZeroAndPrintsVersion()
    {
        var originalOut = Console.Out;

        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);

            var exitCode = await new StressorAppRunner(CreateProvider()).RunAsync(["--version"], TestCancellation.Token);

            Assert.Equal(0, exitCode);
            Assert.Contains("0.5.0-alpha", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_Version_DoesNotRunStressTest()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var originalOut = Console.Out;

        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);

            await new StressorAppRunner(CreateProvider(stressTestRunner)).RunAsync(["--version"], TestCancellation.Token);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseOmitted_BindsOff()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs());

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Verbose == VerboseMode.Off),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseFailures_BindsFailures()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(verbose: "failures"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Verbose == VerboseMode.Failures),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseFull_BindsFull()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(verbose: "full"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Verbose == VerboseMode.Full),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseShortFlag_BindsFailures()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "-v", "failures"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Verbose == VerboseMode.Failures),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseFull_ExitCodeUnchanged()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var options = CreateOptions() with { Verbose = VerboseMode.Full };
        var report = new SessionReport(options, [new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)], false);
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>()).Returns(report);

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(verbose: "full"));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_VerboseAlone_ReturnsError()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var args = CreateArgs();
        args = [.. args, "--verbose"];

        var exitCode = await ExecuteWithRunner(stressTestRunner, args);

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseUnknown_ReturnsError()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(verbose: "unknown"));

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_PrettyPrint_ReturnsError()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var args = CreateArgs();
        args = [.. args, "--prettyprint"];

        var exitCode = await ExecuteWithRunner(stressTestRunner, args);

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("failures", VerboseMode.Failures)]
    [InlineData("FAILURES", VerboseMode.Failures)]
    [InlineData("full", VerboseMode.Full)]
    [InlineData("FULL", VerboseMode.Full)]
    public void TryParseVerboseMode_ParsesKnownModes(string value, VerboseMode expected)
    {
        Assert.True(StressorAppRunner.TryParseVerboseMode(value, out var mode));
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void TryParseVerboseMode_Null_ReturnsOff()
    {
        Assert.True(StressorAppRunner.TryParseVerboseMode(null, out var mode));
        Assert.Equal(VerboseMode.Off, mode);
    }

    [Fact]
    public void TryParseVerboseMode_Unknown_ReturnsFalse()
    {
        Assert.False(StressorAppRunner.TryParseVerboseMode("unknown", out _));
    }

    [Fact]
    public async Task RunAsync_TimeoutOmitted_BindsDefault100Seconds()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs());

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.RequestTimeout == TimeSpan.FromSeconds(100)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_TimeoutProvided_BindsParsedValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "--timeout", "30s"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.RequestTimeout == TimeSpan.FromSeconds(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_TimeoutShortFlag_BindsParsedValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "-t", "30s"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.RequestTimeout == TimeSpan.FromSeconds(30)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("500ms", 500)]
    [InlineData("2.5s", 2500)]
    [InlineData("00:01:40", 100000)]
    public async Task RunAsync_TimeoutFormats_BindParsedValues(string timeout, double expectedMilliseconds)
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "--timeout", timeout];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.RequestTimeout == TimeSpan.FromMilliseconds(expectedMilliseconds)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_TimeoutGreaterThan100Seconds_BindsParsedValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "--timeout", "120s"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.RequestTimeout == TimeSpan.FromSeconds(120)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("0s")]
    [InlineData("0ms")]
    [InlineData("00:00:00")]
    public async Task RunAsync_ZeroTimeout_ReturnsExitCodeOne(string timeout)
    {
        var args = CreateArgs();
        args = [.. args, "--timeout", timeout];

        var exitCode = await ExecuteWithRunner(Substitute.For<IStressTestRunner>(), args);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_InvalidTimeout_ReturnsExitCodeOne()
    {
        var args = CreateArgs();
        args = [.. args, "--timeout", "invalid"];

        var exitCode = await ExecuteWithRunner(Substitute.For<IStressTestRunner>(), args);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_CycleIntervalOmitted_BindsZero()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs());

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.CycleInterval == TimeSpan.Zero),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CycleIntervalProvided_BindsParsedValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "--cycle-interval", "30s"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.CycleInterval == TimeSpan.FromSeconds(30)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("500ms", 500)]
    [InlineData("2.5s", 2500)]
    [InlineData("00:00:30", 30000)]
    public async Task RunAsync_CycleIntervalFormats_BindParsedValues(string cycleInterval, double expectedMilliseconds)
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "--cycle-interval", cycleInterval];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.CycleInterval == TimeSpan.FromMilliseconds(expectedMilliseconds)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("0s")]
    [InlineData("0ms")]
    [InlineData("00:00:00")]
    public async Task RunAsync_ZeroCycleInterval_BindsZero(string cycleInterval)
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "--cycle-interval", cycleInterval];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.CycleInterval == TimeSpan.Zero),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("-1s")]
    public async Task RunAsync_InvalidCycleInterval_ReturnsExitCodeOne(string cycleInterval)
    {
        var args = CreateArgs();
        args = [.. args, "--cycle-interval", cycleInterval];

        var exitCode = await ExecuteWithRunner(Substitute.For<IStressTestRunner>(), args);

        Assert.Equal(1, exitCode);
    }

    [Theory]
    [InlineData("1s", 1000)]
    [InlineData("2.5s", 2500)]
    [InlineData("500ms", 500)]
    [InlineData("250ms", 250)]
    public void TryParseInterval_ValidValues_ReturnsTrue(string value, double expectedMilliseconds)
    {
        Assert.True(StressorAppRunner.TryParseInterval(value, out var interval));
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), interval);
    }

    [Theory]
    [InlineData("00:00:01", 1000)]
    [InlineData("00:00:00.500", 500)]
    public void TryParseInterval_TimeSpanFormat_ReturnsTrue(string value, double expectedMilliseconds)
    {
        Assert.True(StressorAppRunner.TryParseInterval(value, out var interval));
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), interval);
    }

    [Theory]
    [InlineData("0s")]
    [InlineData("0ms")]
    [InlineData("00:00:00")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("ms")]
    [InlineData("s")]
    public void TryParseInterval_InvalidValues_ReturnsFalse(string value)
    {
        Assert.False(StressorAppRunner.TryParseInterval(value, out _));
    }

    [Fact]
    public async Task RunAsync_LoadOmitted_BindsGentlePacing()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs());

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Load == LoadMode.GentlePacing),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LoadGentlePacing_BindsGentlePacing()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "gentle-pacing"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Load == LoadMode.GentlePacing),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LoadFixedRate_BindsFixedRate()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "fixed-rate"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Load == LoadMode.FixedRate),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LoadShortFlag_BindsFixedRate()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "-l", "fixed-rate"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Load == LoadMode.FixedRate),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_InvalidLoad_ReturnsExitCodeOne()
    {
        var exitCode = await ExecuteWithRunner(
            Substitute.For<IStressTestRunner>(),
            CreateArgs(load: "burst"));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_CyclesOmitted_UsesOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(cycles: null));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Cycles == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CyclesExplicitOne_BindsOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(cycles: "1"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Cycles == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CyclesExplicitMultiple_BindsValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(cycles: "60"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Cycles == 60),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CyclesShortForm_BindsValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs(cycles: null);
        args = [.. args, "-c", "5"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Cycles == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CyclesZero_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(cycles: "0"));

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CyclesNegative_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(cycles: "-1"));

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_CyclesNonNumeric_ReturnsNonZeroExitCode()
    {
        var exitCode = await ExecuteWithRunner(
            Substitute.For<IStressTestRunner>(),
            CreateArgs(cycles: "abc"));

        Assert.NotEqual(0, exitCode);
    }

    [Theory]
    [InlineData("gentle-pacing", LoadMode.GentlePacing)]
    [InlineData("GENTLE-PACING", LoadMode.GentlePacing)]
    [InlineData("fixed-rate", LoadMode.FixedRate)]
    [InlineData("Fixed-Rate", LoadMode.FixedRate)]
    [InlineData("batch", LoadMode.Batch)]
    [InlineData("BATCH", LoadMode.Batch)]
    public void TryParseLoadMode_ValidValues_ReturnsTrue(string value, LoadMode expected)
    {
        Assert.True(StressorAppRunner.TryParseLoadMode(value, out var loadMode));
        Assert.Equal(expected, loadMode);
    }

    [Theory]
    [InlineData("burst")]
    [InlineData("")]
    [InlineData("fixed")]
    public void TryParseLoadMode_InvalidValues_ReturnsFalse(string value)
    {
        Assert.False(StressorAppRunner.TryParseLoadMode(value, out _));
    }

    [Fact]
    public async Task RunAsync_InvalidInterval_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var args = CreateArgs();
        var intervalIndex = Array.IndexOf(args, "--interval");
        args[intervalIndex + 1] = "not-a-duration";

        var exitCode = await ExecuteWithRunner(stressTestRunner, args);

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void MapExitCode_AllSuccess_ReturnsZero()
    {
        var options = CreateOptions();
        var report = new SessionReport(
            options,
            [new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)],
            false);

        Assert.Equal(0, StressorAppRunner.MapExitCode(report));
    }

    [Fact]
    public void MapExitCode_FailuresOnly_ReturnsOne()
    {
        var options = CreateOptions();
        var report = new SessionReport(
            options,
            [new RequestOutcome(1, 1, false, false, 500, TimeSpan.Zero, "error")],
            false);

        Assert.Equal(1, StressorAppRunner.MapExitCode(report));
    }

    [Fact]
    public void MapExitCode_CancelledWithFailures_ReturnsTwo()
    {
        var options = CreateOptions();
        var report = new SessionReport(
            options,
            [
                new RequestOutcome(1, 1, false, false, 500, TimeSpan.Zero, "error"),
                new RequestOutcome(1, 2, false, true, null, TimeSpan.Zero, "cancelled")
            ],
            true);

        Assert.Equal(2, StressorAppRunner.MapExitCode(report));
    }

    [Fact]
    public async Task RunAsync_CancelledWithFailures_ReturnsExitCodeTwo()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var options = CreateOptions();
        var report = new SessionReport(
            options,
            [
                new RequestOutcome(1, 1, false, false, 500, TimeSpan.Zero, "error"),
                new RequestOutcome(1, 2, false, true, null, TimeSpan.Zero, "cancelled")
            ],
            true);
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>()).Returns(report);

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs());

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_BatchOmitted_BindsBatchOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs());

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Batch == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_BatchExplicit_BindsValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "batch", batch: "5", requests: "10"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Batch == 5 && o.Load == LoadMode.Batch),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_BatchShortFlag_BindsValue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs(load: "batch", requests: "10");
        args = [.. args, "-b", "5"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Batch == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_LoadBatch_BindsBatchLoad()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "batch", batch: "1"));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Load == LoadMode.Batch),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_BatchOneWithGentlePacing_ReturnsExitCodeZero()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(new SessionReport(CreateOptions(), [], false));

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(batch: "1"));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_BatchGreaterThanOneWithoutBatchLoad_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(batch: "5", requests: "10"));

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_BatchGreaterThanOneWithFixedRate_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "fixed-rate", batch: "5", requests: "10"));

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_BatchGreaterThanRequests_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "batch", batch: "11", requests: "10"));

        Assert.Equal(1, exitCode);
        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_BatchEqualToRequests_ReturnsExitCodeZero()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "batch", batch: "10", requests: "10"));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_BatchZero_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "batch", batch: "0"));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_BatchNegative_ReturnsExitCodeOne()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(load: "batch", batch: "-1"));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_BatchNonNumeric_ReturnsNonZeroExitCode()
    {
        var exitCode = await ExecuteWithRunner(
            Substitute.For<IStressTestRunner>(),
            CreateArgs(load: "batch", batch: "abc"));

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_BatchLoadWithZeroInterval_ReturnsExitCodeZero()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs(load: "batch", batch: "5", requests: "10");
        var intervalIndex = Array.IndexOf(args, "--interval");
        args[intervalIndex + 1] = "0s";

        var exitCode = await ExecuteWithRunner(stressTestRunner, args);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_BatchLoadWithZeroInterval_BindsZeroInterval()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs(load: "batch", batch: "5", requests: "10");
        var intervalIndex = Array.IndexOf(args, "--interval");
        args[intervalIndex + 1] = "0s";

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Interval == TimeSpan.Zero && o.Load == LoadMode.Batch),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("0s")]
    [InlineData("0ms")]
    [InlineData("00:00:00")]
    public void TryParseInterval_ZeroValues_ReturnsTrueWhenAllowZero(string value)
    {
        Assert.True(StressorAppRunner.TryParseInterval(value, allowZero: true, out var interval));
        Assert.Equal(TimeSpan.Zero, interval);
    }

    [Theory]
    [InlineData("0s")]
    [InlineData("0ms")]
    [InlineData("00:00:00")]
    public void TryParseInterval_ZeroValues_ReturnsFalseWhenAllowZeroFalse(string value)
    {
        Assert.False(StressorAppRunner.TryParseInterval(value, allowZero: false, out _));
    }

    private static async Task<int> ExecuteWithRunner(IStressTestRunner stressTestRunner, string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton(stressTestRunner);
        var provider = services.BuildServiceProvider();
        return await new StressorAppRunner(provider).RunAsync(args, TestCancellation.Token);
    }

    private static IServiceProvider CreateProvider(IStressTestRunner? stressTestRunner = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(stressTestRunner ?? Substitute.For<IStressTestRunner>());
        return services.BuildServiceProvider();
    }

    private static string[] CreateArgs(string? method = "POST", string? auth = null, string? verbose = null, string? load = null, string? cycles = null, string? batch = null, string? requests = null)
    {
        var args = new List<string>
        {
            "--url", "https://example.com",
            "--payload", "payload.json",
            "--requests", requests ?? "1",
            "--interval", "1s"
        };

        if (cycles is not null)
        {
            args.Add("--cycles");
            args.Add(cycles);
        }

        if (method is not null)
        {
            args.Add("--method");
            args.Add(method);
        }

        if (auth is not null)
        {
            args.Add("--auth");
            args.Add(auth);
        }

        if (verbose is not null)
        {
            args.Add("--verbose");
            args.Add(verbose);
        }

        if (load is not null)
        {
            args.Add("--load");
            args.Add(load);
        }

        if (batch is not null)
        {
            args.Add("--batch");
            args.Add(batch);
        }

        return [.. args];
    }

    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, 1, TimeSpan.FromSeconds(1), 1);
}
