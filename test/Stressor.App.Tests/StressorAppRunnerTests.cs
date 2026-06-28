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
            ["--payload", "payload.json", "--requests", "1", "--interval", "1s", "--cycles", "1"]);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_MissingPayload_ReturnsNonZeroExitCode()
    {
        var exitCode = await new StressorAppRunner(CreateProvider()).RunAsync(
            ["--url", "https://example.com", "--requests", "1", "--interval", "1s", "--cycles", "1"]);

        Assert.NotEqual(0, exitCode);
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

            var exitCode = await new StressorAppRunner(CreateProvider()).RunAsync([helpArg]);

            Assert.Equal(0, exitCode);

            var output = writer.ToString();
            Assert.Contains("Stress tests an API endpoint.", output);
            Assert.Contains("--url", output);
            Assert.Contains("Examples:", output);
            Assert.Contains("Exit codes:", output);
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

            await new StressorAppRunner(CreateProvider(stressTestRunner)).RunAsync(["--help"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        await stressTestRunner.DidNotReceive().RunAsync(
            Arg.Any<StressTestOptions>(),
            Arg.Any<CancellationToken>());
    }

    private static async Task<int> ExecuteWithRunner(IStressTestRunner stressTestRunner, string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton(stressTestRunner);
        var provider = services.BuildServiceProvider();
        return await new StressorAppRunner(provider).RunAsync(args);
    }

    private static IServiceProvider CreateProvider(IStressTestRunner? stressTestRunner = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(stressTestRunner ?? Substitute.For<IStressTestRunner>());
        return services.BuildServiceProvider();
    }

    private static string[] CreateArgs(string? method = "POST", string? auth = null)
    {
        var args = new List<string>
        {
            "--url", "https://example.com",
            "--payload", "payload.json",
            "--requests", "1",
            "--interval", "1s",
            "--cycles", "1"
        };

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

        return [.. args];
    }

    private static StressTestOptions CreateOptions() =>
        new(new Uri("https://example.com"), "payload.json", HttpMethod.Post, 1, TimeSpan.FromSeconds(1), 1);
}
