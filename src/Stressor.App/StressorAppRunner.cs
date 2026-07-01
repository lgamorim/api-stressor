namespace Stressor.App;

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Stressor.Core;

public sealed class StressorAppRunner
{
    private readonly IServiceProvider serviceProvider;

    public StressorAppRunner(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler? cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            linkedCts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            var rootCommand = BuildRootCommand();
            var parseResult = rootCommand.Parse(args);

            if (parseResult.Errors.Count > 0 && !IsHelpRequested(args))
            {
                foreach (var error in parseResult.Errors)
                {
                    await Console.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
                }

                return 1;
            }

            return await parseResult.InvokeAsync(cancellationToken: linkedCts.Token).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    internal static bool IsHelpRequested(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                case "-?":
                case "/?":
                case "/h":
                    return true;
            }
        }

        return false;
    }

    internal RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("Stress tests an API endpoint.");

        var urlOption = new Option<string>("--url", "-u")
        {
            Description = "API endpoint URL",
            Required = true
        };
        var payloadOption = new Option<string>("--payload", "-p")
        {
            Description = "Path to a JSON payload file (single body or multi-payload envelope)",
            Required = true
        };
        var methodOption = new Option<string>("--method", "-m")
        {
            Description = "HTTP method (default: POST)",
            DefaultValueFactory = _ => "POST"
        };
        var requestsOption = new Option<int>("--requests", "-r")
        {
            Description = "Requests to send per cycle",
            Required = true
        };
        var intervalOption = new Option<string>("--interval", "-i")
        {
            Description = "Minimum delay between consecutive request starts (e.g. 1s, 500ms, 00:00:01)",
            Required = true
        };
        var cyclesOption = new Option<int>("--cycles", "-c")
        {
            Description = "Number of cycles to execute",
            Required = true
        };
        var authOption = new Option<string?>("--auth", "-a")
        {
            Description = "Authorization header value (e.g. Bearer <token>)"
        };
        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Print per-request position, payload, and failure details"
        };
        var prettyPrintOption = new Option<bool>("--prettyprint", "-pp")
        {
            Description = "Print per-request output with indented JSON payloads"
        };

        rootCommand.Options.Add(urlOption);
        rootCommand.Options.Add(payloadOption);
        rootCommand.Options.Add(methodOption);
        rootCommand.Options.Add(requestsOption);
        rootCommand.Options.Add(intervalOption);
        rootCommand.Options.Add(cyclesOption);
        rootCommand.Options.Add(authOption);
        rootCommand.Options.Add(verboseOption);
        rootCommand.Options.Add(prettyPrintOption);

        rootCommand.SetAction(async (parseResult, token) =>
        {
            return await ExecuteAsync(
                parseResult.GetValue(urlOption)!,
                parseResult.GetValue(payloadOption)!,
                parseResult.GetValue(methodOption)!,
                parseResult.GetValue(requestsOption),
                parseResult.GetValue(intervalOption)!,
                parseResult.GetValue(cyclesOption),
                parseResult.GetValue(authOption),
                parseResult.GetValue(verboseOption),
                parseResult.GetValue(prettyPrintOption),
                token).ConfigureAwait(false);
        });

        StressorAppHelp.Configure(rootCommand);

        return rootCommand;
    }

    internal static async Task<int> ExecuteAsync(
        string url,
        string payloadPath,
        string method,
        int requests,
        string interval,
        int cycles,
        string? auth,
        bool verbose,
        bool prettyPrint,
        CancellationToken cancellationToken,
        IServiceProvider? serviceProviderOverride = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Console.Error.WriteLineAsync("URL must be absolute.").ConfigureAwait(false);
            return 1;
        }

        if (!TryParseInterval(interval, out var intervalSpan))
        {
            await Console.Error.WriteLineAsync("Interval must be a valid duration (e.g. 1s, 500ms, 00:00:01).").ConfigureAwait(false);
            return 1;
        }

        var options = new StressTestOptions(
            uri,
            payloadPath,
            new HttpMethod(method),
            requests,
            intervalSpan,
            cycles,
            auth,
            verbose,
            prettyPrint);

        var validationErrors = StressTestOptionsValidator.Validate(options);
        if (validationErrors.Count > 0)
        {
            foreach (var error in validationErrors)
            {
                await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            }

            return 1;
        }

        var provider = serviceProviderOverride ?? throw new InvalidOperationException("Service provider is required.");
        var runner = provider.GetRequiredService<IStressTestRunner>();

        try
        {
            var report = await runner.RunAsync(options, cancellationToken).ConfigureAwait(false);
            return MapExitCode(report);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private async Task<int> ExecuteAsync(
        string url,
        string payloadPath,
        string method,
        int requests,
        string interval,
        int cycles,
        string? auth,
        bool verbose,
        bool prettyPrint,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            url,
            payloadPath,
            method,
            requests,
            interval,
            cycles,
            auth,
            verbose,
            prettyPrint,
            cancellationToken,
            serviceProvider).ConfigureAwait(false);
    }

    internal static int MapExitCode(SessionReport report)
    {
        if (report.WasCancelled)
        {
            return 2;
        }

        return report.FailedCount == 0 ? 0 : 1;
    }

    internal static bool TryParseInterval(string value, out TimeSpan interval)
    {
        if (TimeSpan.TryParse(value, out interval))
        {
            return interval > TimeSpan.Zero;
        }

        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var milliseconds))
        {
            interval = TimeSpan.FromMilliseconds(milliseconds);
            return interval > TimeSpan.Zero;
        }

        if (value.EndsWith('s') && !value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            interval = TimeSpan.FromSeconds(seconds);
            return interval > TimeSpan.Zero;
        }

        interval = default;
        return false;
    }
}
