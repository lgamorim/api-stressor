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

            if (parseResult.Errors.Count > 0 && !IsHelpRequested(args) && !IsVersionRequested(args))
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

    internal static bool IsVersionRequested(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], "--version", StringComparison.Ordinal))
            {
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
            Description = "Number of cycles to execute (default: 1)",
            DefaultValueFactory = _ => 1
        };
        var authOption = new Option<string?>("--auth", "-a")
        {
            Description = "Authorization header value (e.g. Bearer <token>)"
        };
        var verboseOption = new Option<string?>("--verbose", "-v")
        {
            Description = "Per-request output mode: failures or full"
        };
        var loadOption = new Option<string>("--load", "-l")
        {
            Description = "Load handling mode: gentle-pacing (default), fixed-rate, or batch",
            DefaultValueFactory = _ => "gentle-pacing"
        };
        var batchOption = new Option<int>("--batch", "-b")
        {
            Description = "Max parallel requests per wave (default: 1; use with --load batch)",
            DefaultValueFactory = _ => 1
        };
        var timeoutOption = new Option<string>("--timeout", "-t")
        {
            Description = "Per-request timeout (default: 100s; e.g. 30s, 500ms, 00:01:40)",
            DefaultValueFactory = _ => "100s"
        };
        var cycleIntervalOption = new Option<string>("--cycle-interval")
        {
            Description = "Minimum wait after a cycle completes before the next cycle starts (default: 0s)",
            DefaultValueFactory = _ => "0s"
        };

        rootCommand.Options.Add(urlOption);
        rootCommand.Options.Add(payloadOption);
        rootCommand.Options.Add(methodOption);
        rootCommand.Options.Add(requestsOption);
        rootCommand.Options.Add(intervalOption);
        rootCommand.Options.Add(authOption);
        rootCommand.Options.Add(verboseOption);
        rootCommand.Options.Add(loadOption);
        rootCommand.Options.Add(batchOption);
        rootCommand.Options.Add(timeoutOption);
        rootCommand.Options.Add(cyclesOption);
        rootCommand.Options.Add(cycleIntervalOption);

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
                parseResult.GetValue(loadOption)!,
                parseResult.GetValue(batchOption),
                parseResult.GetValue(timeoutOption)!,
                parseResult.GetValue(cycleIntervalOption)!,
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
        string? verbose,
        string load,
        int batch,
        string timeout,
        string cycleInterval,
        CancellationToken cancellationToken,
        IServiceProvider? serviceProviderOverride = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Console.Error.WriteLineAsync("URL must be absolute.").ConfigureAwait(false);
            return 1;
        }

        if (!TryParseLoadMode(load, out var loadMode))
        {
            await Console.Error.WriteLineAsync("Load must be gentle-pacing, fixed-rate, or batch.").ConfigureAwait(false);
            return 1;
        }

        if (!TryParseInterval(interval, allowZero: loadMode == LoadMode.Batch, out var intervalSpan))
        {
            await Console.Error.WriteLineAsync("Interval must be a valid duration (e.g. 1s, 500ms, 00:00:01).").ConfigureAwait(false);
            return 1;
        }

        if (!TryParseInterval(timeout, allowZero: false, out var timeoutSpan))
        {
            await Console.Error.WriteLineAsync("Timeout must be a valid duration (e.g. 30s, 500ms, 00:01:40).").ConfigureAwait(false);
            return 1;
        }

        if (!TryParseInterval(cycleInterval, allowZero: true, out var cycleIntervalSpan))
        {
            await Console.Error.WriteLineAsync("Cycle interval must be a valid duration (e.g. 30s, 500ms, 00:00:30).").ConfigureAwait(false);
            return 1;
        }

        if (!TryParseVerboseMode(verbose, out var verboseMode))
        {
            await Console.Error.WriteLineAsync("Verbose must be failures or full.").ConfigureAwait(false);
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
            verboseMode,
            loadMode,
            batch)
        {
            RequestTimeout = timeoutSpan,
            CycleInterval = cycleIntervalSpan
        };

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
        string? verbose,
        string load,
        int batch,
        string timeout,
        string cycleInterval,
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
            load,
            batch,
            timeout,
            cycleInterval,
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

    internal static bool TryParseInterval(string value, bool allowZero, out TimeSpan interval)
    {
        if (TimeSpan.TryParse(value, out interval))
        {
            return allowZero ? interval >= TimeSpan.Zero : interval > TimeSpan.Zero;
        }

        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var milliseconds))
        {
            interval = TimeSpan.FromMilliseconds(milliseconds);
            return allowZero ? interval >= TimeSpan.Zero : interval > TimeSpan.Zero;
        }

        if (value.EndsWith('s') && !value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            interval = TimeSpan.FromSeconds(seconds);
            return allowZero ? interval >= TimeSpan.Zero : interval > TimeSpan.Zero;
        }

        interval = default;
        return false;
    }

    internal static bool TryParseInterval(string value, out TimeSpan interval) =>
        TryParseInterval(value, allowZero: false, out interval);

    internal static bool TryParseLoadMode(string value, out LoadMode loadMode)
    {
        if (string.Equals(value, "gentle-pacing", StringComparison.OrdinalIgnoreCase))
        {
            loadMode = LoadMode.GentlePacing;
            return true;
        }

        if (string.Equals(value, "fixed-rate", StringComparison.OrdinalIgnoreCase))
        {
            loadMode = LoadMode.FixedRate;
            return true;
        }

        if (string.Equals(value, "batch", StringComparison.OrdinalIgnoreCase))
        {
            loadMode = LoadMode.Batch;
            return true;
        }

        loadMode = default;
        return false;
    }

    internal static bool TryParseVerboseMode(string? value, out VerboseMode verboseMode)
    {
        if (value is null)
        {
            verboseMode = VerboseMode.Off;
            return true;
        }

        if (string.Equals(value, "failures", StringComparison.OrdinalIgnoreCase))
        {
            verboseMode = VerboseMode.Failures;
            return true;
        }

        if (string.Equals(value, "full", StringComparison.OrdinalIgnoreCase))
        {
            verboseMode = VerboseMode.Full;
            return true;
        }

        verboseMode = VerboseMode.Off;
        return false;
    }
}
