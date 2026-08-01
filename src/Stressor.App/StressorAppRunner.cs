namespace Stressor.App;

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Stressor.Core;

/// <summary>Parses CLI arguments and runs stress-test sessions.</summary>
public sealed class StressorAppRunner
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Creates a runner that resolves services from the given provider.</summary>
    public StressorAppRunner(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>Parses arguments and runs the CLI, returning the process exit code.</summary>
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

        var configOption = new Option<string?>("--config", "-f")
        {
            Description = "Path to a JSON scenario config file"
        };
        var urlOption = new Option<string?>("--url", "-u")
        {
            Description = "API endpoint URL"
        };
        var payloadOption = new Option<string?>("--payload", "-p")
        {
            Description = "Path to a JSON payload file (single body or multi-payload envelope)"
        };
        var methodOption = new Option<string>("--method", "-m")
        {
            Description = "HTTP method (default: POST)",
            DefaultValueFactory = _ => "POST"
        };
        var requestsOption = new Option<int?>("--requests", "-r")
        {
            Description = "Requests to send per cycle"
        };
        var intervalOption = new Option<string?>("--interval", "-i")
        {
            Description = "Minimum delay between consecutive request starts (e.g. 1s, 500ms, 00:00:01)"
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
        var headerOption = new Option<string[]>("--header", "-H")
        {
            Description = "Request header (Name: Value). Repeatable."
        };
        var headersFileOption = new Option<string?>("--headers")
        {
            Description = "Path to a JSON file of HTTP headers"
        };
        var expectStatusOption = new Option<string[]>("--expect-status")
        {
            Description = "HTTP status code that counts as success (repeatable; comma-separated allowed)"
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
        var durationOption = new Option<string?>("--duration")
        {
            Description = "Wall-clock session duration (alternative to --cycles; e.g. 5m, 300s, 00:05:00)"
        };
        var reportOption = new Option<string?>("--report")
        {
            Description = "Path to write a JSON session report after completion"
        };

        rootCommand.Options.Add(configOption);
        rootCommand.Options.Add(urlOption);
        rootCommand.Options.Add(payloadOption);
        rootCommand.Options.Add(methodOption);
        rootCommand.Options.Add(requestsOption);
        rootCommand.Options.Add(intervalOption);
        rootCommand.Options.Add(authOption);
        rootCommand.Options.Add(headerOption);
        rootCommand.Options.Add(headersFileOption);
        rootCommand.Options.Add(expectStatusOption);
        rootCommand.Options.Add(verboseOption);
        rootCommand.Options.Add(loadOption);
        rootCommand.Options.Add(batchOption);
        rootCommand.Options.Add(timeoutOption);
        rootCommand.Options.Add(cyclesOption);
        rootCommand.Options.Add(cycleIntervalOption);
        rootCommand.Options.Add(durationOption);
        rootCommand.Options.Add(reportOption);

        rootCommand.SetAction(async (parseResult, token) =>
        {
            var configPath = parseResult.GetValue(configOption);
            StressTestScenarioDocument? document = null;

            if (!string.IsNullOrWhiteSpace(configPath))
            {
                try
                {
                    var reader = _serviceProvider.GetRequiredService<IStressTestScenarioReader>();
                    document = await reader.ReadAsync(configPath, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
                    return 1;
                }
            }

            var cliOverrides = BuildCliOverrides(
                parseResult,
                urlOption,
                payloadOption,
                methodOption,
                requestsOption,
                intervalOption,
                cyclesOption,
                authOption,
                headerOption,
                headersFileOption,
                expectStatusOption,
                verboseOption,
                loadOption,
                batchOption,
                timeoutOption,
                cycleIntervalOption,
                durationOption,
                reportOption);

            var configuration = StressTestConfigurationMerger.Merge(document, configPath, cliOverrides);

            try
            {
                configuration = await ApplyHeaderLayersAsync(configuration, cliOverrides, token).ConfigureAwait(false);
                configuration = ApplyExpectStatusOverride(configuration, cliOverrides);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
                return 1;
            }

            return await ExecuteAsync(configuration, token, _serviceProvider).ConfigureAwait(false);
        });

        StressorAppHelp.Configure(rootCommand);

        return rootCommand;
    }

    internal async Task<StressTestConfigurationValues> ApplyHeaderLayersAsync(
        StressTestConfigurationValues configuration,
        StressTestCliOverrides cliOverrides,
        CancellationToken cancellationToken)
    {
        var headers = configuration.Headers;

        if (cliOverrides.IsSpecified(StressTestConfigurationOptionNames.HeadersFile)
            && !string.IsNullOrWhiteSpace(cliOverrides.HeadersFile))
        {
            var headersReader = _serviceProvider.GetRequiredService<IHttpHeadersReader>();
            var fileHeaders = await headersReader.ReadAsync(cliOverrides.HeadersFile, cancellationToken).ConfigureAwait(false);
            headers = HttpHeadersMerger.Merge(headers, fileHeaders);
        }

        if (cliOverrides.IsSpecified(StressTestConfigurationOptionNames.Header) && cliOverrides.Header.Count > 0)
        {
            var cliHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in cliOverrides.Header)
            {
                if (!HttpHeaderParser.TryParse(header, out var name, out var value))
                {
                    throw new ArgumentException("Header must be in Name: Value format.");
                }

                cliHeaders[name] = value;
            }

            headers = HttpHeadersMerger.Merge(headers, cliHeaders);
        }

        return configuration with { Headers = headers };
    }

    internal static StressTestConfigurationValues ApplyExpectStatusOverride(
        StressTestConfigurationValues configuration,
        StressTestCliOverrides cliOverrides)
    {
        if (!cliOverrides.IsSpecified(StressTestConfigurationOptionNames.ExpectStatus))
        {
            return configuration;
        }

        if (!ExpectedStatusCodeParser.TryParseMany(cliOverrides.ExpectStatus, out var codes, out var error))
        {
            throw new ArgumentException(error);
        }

        return configuration with { ExpectStatus = codes };
    }

    internal static StressTestCliOverrides BuildCliOverrides(
        ParseResult parseResult,
        Option<string?> urlOption,
        Option<string?> payloadOption,
        Option<string> methodOption,
        Option<int?> requestsOption,
        Option<string?> intervalOption,
        Option<int> cyclesOption,
        Option<string?> authOption,
        Option<string[]> headerOption,
        Option<string?> headersFileOption,
        Option<string[]> expectStatusOption,
        Option<string?> verboseOption,
        Option<string> loadOption,
        Option<int> batchOption,
        Option<string> timeoutOption,
        Option<string> cycleIntervalOption,
        Option<string?> durationOption,
        Option<string?> reportOption)
    {
        var specified = new HashSet<string>();

        if (IsOptionSpecified(parseResult, urlOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Url);
        }

        if (IsOptionSpecified(parseResult, payloadOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Payload);
        }

        if (IsOptionSpecified(parseResult, methodOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Method);
        }

        if (IsOptionSpecified(parseResult, requestsOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Requests);
        }

        if (IsOptionSpecified(parseResult, intervalOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Interval);
        }

        if (IsOptionSpecified(parseResult, cyclesOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Cycles);
        }

        if (IsOptionSpecified(parseResult, authOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Auth);
        }

        if (IsOptionSpecified(parseResult, headerOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Header);
        }

        if (IsOptionSpecified(parseResult, headersFileOption))
        {
            specified.Add(StressTestConfigurationOptionNames.HeadersFile);
        }

        if (IsOptionSpecified(parseResult, expectStatusOption))
        {
            specified.Add(StressTestConfigurationOptionNames.ExpectStatus);
        }

        if (IsOptionSpecified(parseResult, verboseOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Verbose);
        }

        if (IsOptionSpecified(parseResult, loadOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Load);
        }

        if (IsOptionSpecified(parseResult, batchOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Batch);
        }

        if (IsOptionSpecified(parseResult, timeoutOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Timeout);
        }

        if (IsOptionSpecified(parseResult, cycleIntervalOption))
        {
            specified.Add(StressTestConfigurationOptionNames.CycleInterval);
        }

        if (IsOptionSpecified(parseResult, durationOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Duration);
        }

        if (IsOptionSpecified(parseResult, reportOption))
        {
            specified.Add(StressTestConfigurationOptionNames.Report);
        }

        return new StressTestCliOverrides
        {
            SpecifiedOptions = specified,
            Url = parseResult.GetValue(urlOption),
            Payload = parseResult.GetValue(payloadOption),
            Method = parseResult.GetValue(methodOption),
            Requests = parseResult.GetValue(requestsOption),
            Interval = parseResult.GetValue(intervalOption),
            Cycles = parseResult.GetValue(cyclesOption),
            Auth = parseResult.GetValue(authOption),
            HeadersFile = parseResult.GetValue(headersFileOption),
            Header = parseResult.GetValue(headerOption) ?? [],
            ExpectStatus = parseResult.GetValue(expectStatusOption) ?? [],
            Verbose = parseResult.GetValue(verboseOption),
            Load = parseResult.GetValue(loadOption),
            Batch = parseResult.GetValue(batchOption),
            Timeout = parseResult.GetValue(timeoutOption),
            CycleInterval = parseResult.GetValue(cycleIntervalOption),
            Duration = parseResult.GetValue(durationOption),
            Report = parseResult.GetValue(reportOption)
        };
    }

    internal static bool IsOptionSpecified<T>(ParseResult parseResult, Option<T> option)
    {
        var result = parseResult.GetResult(option);
        return result is not null && !result.Implicit;
    }

    internal static async Task<int> ExecuteAsync(
        StressTestConfigurationValues configuration,
        CancellationToken cancellationToken,
        IServiceProvider? serviceProviderOverride = null)
    {
        var (options, errors) = StressTestOptionParser.TryCreateOptions(configuration);
        if (options is null)
        {
            foreach (var error in errors)
            {
                await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            }

            return 1;
        }

        return await ExecuteAsync(options, cancellationToken, serviceProviderOverride).ConfigureAwait(false);
    }

    internal static async Task<int> ExecuteAsync(
        StressTestOptions options,
        CancellationToken cancellationToken,
        IServiceProvider? serviceProviderOverride = null)
    {
        var provider = serviceProviderOverride ?? throw new InvalidOperationException("Service provider is required.");
        var runner = provider.GetRequiredService<IStressTestRunner>();

        try
        {
            var report = await runner.RunAsync(options, cancellationToken).ConfigureAwait(false);
            var exitCode = MapExitCode(report);

            if (!string.IsNullOrWhiteSpace(options.ReportFilePath))
            {
                try
                {
                    var reportWriter = provider.GetRequiredService<IJsonSessionReportWriter>();
                    await reportWriter.WriteAsync(
                        report,
                        options.ReportFilePath,
                        exitCode,
                        StressorAppHelp.GetAppVersion(),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
                    return 1;
                }
            }

            return exitCode;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }
    }

    internal static int MapExitCode(SessionReport report)
    {
        if (report.WasCancelled)
        {
            return 2;
        }

        return report.FailedCount == 0 ? 0 : 1;
    }
}
