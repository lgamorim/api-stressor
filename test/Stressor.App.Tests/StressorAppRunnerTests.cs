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
    public async Task RunAsync_VerboseOmitted_BindsFalse()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs());

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => !o.Verbose),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseFlag_BindsTrue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(verbose: true));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Verbose),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseShortFlag_BindsTrue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "-v"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.Verbose),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_VerboseTrue_ExitCodeUnchanged()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        var options = CreateOptions() with { Verbose = true };
        var report = new SessionReport(options, [new RequestOutcome(1, 1, true, false, 200, TimeSpan.Zero, null)], false);
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>()).Returns(report);

        var exitCode = await ExecuteWithRunner(stressTestRunner, CreateArgs(verbose: true));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_PrettyPrintOmitted_BindsFalse()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs());

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => !o.PrettyPrint),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_PrettyPrintFlag_BindsTrue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        await ExecuteWithRunner(stressTestRunner, CreateArgs(prettyPrint: true));

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.PrettyPrint),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_PrettyPrintShortFlag_BindsTrue()
    {
        var stressTestRunner = Substitute.For<IStressTestRunner>();
        stressTestRunner.RunAsync(Arg.Any<StressTestOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SessionReport(callInfo.ArgAt<StressTestOptions>(0), [], false));

        var args = CreateArgs();
        args = [.. args, "-pp"];

        await ExecuteWithRunner(stressTestRunner, args);

        await stressTestRunner.Received(1).RunAsync(
            Arg.Is<StressTestOptions>(o => o.PrettyPrint),
            Arg.Any<CancellationToken>());
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

    private static string[] CreateArgs(string? method = "POST", string? auth = null, bool verbose = false, bool prettyPrint = false, string? load = null, string? cycles = null)
    {
        var args = new List<string>
        {
            "--url", "https://example.com",
            "--payload", "payload.json",
            "--requests", "1",
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

        if (verbose)
        {
            args.Add("--verbose");
        }

        if (prettyPrint)
        {
            args.Add("--prettyprint");
        }

        if (load is not null)
        {
            args.Add("--load");
            args.Add(load);
        }

        return [.. args];
    }

    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, 1, TimeSpan.FromSeconds(1), 1);
}
