namespace Stressor.App;

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Reflection;

/// <summary>Configures extended help and version output for the CLI.</summary>
public static class StressorAppHelp
{
    internal static void Configure(RootCommand rootCommand)
    {
        foreach (var option in rootCommand.Options)
        {
            if (option is HelpOption helpOption && helpOption.Action is HelpAction defaultHelp)
            {
                helpOption.Action = new ExtendedHelpAction(defaultHelp);
            }
            else if (option is VersionOption versionOption)
            {
                versionOption.Action = new AppVersionAction();
            }
        }
    }

    internal static string GetAppVersion()
    {
        var assembly = typeof(StressorAppRunner).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    /// <summary>Writes the extended usage guide to the given writer.</summary>
    public static void WriteUsageGuide(TextWriter output)
    {
        output.WriteLine();
        output.WriteLine("Examples:");
        output.WriteLine("  Stressor.App --url https://api.example.com/orders --payload ./payload.json \\");
        output.WriteLine("    --requests 10 --interval 1s");
        output.WriteLine();
        output.WriteLine("  Stressor.App --url https://api.example.com/orders --payload ./payload.json \\");
        output.WriteLine("    --requests 10 --interval 1s --cycles 60");
        output.WriteLine();
        output.WriteLine("  Stressor.App -u https://api.example.com/orders -p ./payloads.json -m POST \\");
        output.WriteLine("    -r 10 -i 1s -c 60 -a \"Bearer your-token-here\"");
        output.WriteLine();
        output.WriteLine("  Stressor.App --config ./scenario.json");
        output.WriteLine();
        output.WriteLine("  Stressor.App --config ./scenario.json --cycles 10");
        output.WriteLine();
        output.WriteLine("  Stressor.App --url https://api.example.com/orders --payload ./payload.json \\");
        output.WriteLine("    --requests 10 --interval 1s --header \"X-Api-Key: abc123\"");
        output.WriteLine();
        output.WriteLine("Scenario config file:");
        output.WriteLine("  Use --config to load settings from a JSON file with the same fields as the CLI options.");
        output.WriteLine("  Explicit CLI flags override config values; omitted flags keep config values.");
        output.WriteLine("  Payload paths in the config file are resolved relative to the config file directory.");
        output.WriteLine("  Scenario config may include a headers object of name/value pairs.");
        output.WriteLine();
        output.WriteLine("Headers:");
        output.WriteLine("  --header \"Name: Value\" (repeatable) adds or overrides a request header.");
        output.WriteLine("  --headers <file.json> loads headers from a JSON object.");
        output.WriteLine("  Precedence: config headers, --headers file, --header flags, then --auth for Authorization.");
        output.WriteLine("  Content-Type in headers overrides the default application/json body type.");
        output.WriteLine();
        output.WriteLine("Expected status codes:");
        output.WriteLine("  --expect-status <code> (repeatable) defines which HTTP status codes count as success.");
        output.WriteLine("  Each flag accepts one code or a comma-separated list (e.g. 200,201).");
        output.WriteLine("  Scenario config may include expectStatus as an array of integers.");
        output.WriteLine("  When omitted, any 2xx response counts as success.");
        output.WriteLine();
        output.WriteLine("Payload file:");
        output.WriteLine("  Single body: any JSON value sent unchanged on every request.");
        output.WriteLine("  Multi-payload: root object with only a \"payloads\" array; items rotate");
        output.WriteLine("    per request within each cycle, wrapping when requests exceed payload count.");
        output.WriteLine();
        output.WriteLine("Load:");
        output.WriteLine("  --load gentle-pacing (default): minimum delay between request starts; waits for each response.");
        output.WriteLine("  --load fixed-rate: starts every interval on a fixed schedule; requests may overlap.");
        output.WriteLine("  --load batch: sends up to --batch requests in parallel per wave; --interval is between wave starts.");
        output.WriteLine("  --batch: max parallel requests per wave (default: 1). Must not exceed --requests.");
        output.WriteLine("  Use --load batch when --batch is greater than 1.");
        output.WriteLine("  --interval is the spacing between consecutive request starts (minimum for gentle-pacing, exact for fixed-rate).");
        output.WriteLine("  In batch mode, --interval is the spacing between wave starts (0s allowed for back-to-back waves).");
        output.WriteLine("  --cycle-interval is the minimum wait after a cycle completes before the next cycle starts (default: 0s).");
        output.WriteLine("  Each cycle sends --requests calls; when --cycle-interval is 0s, pacing continues across cycle boundaries.");
        output.WriteLine("  Total requests in a session = requests x cycles.");
        output.WriteLine("  Use --verbose failures to print detail only for failed or cancelled requests.");
        output.WriteLine("  Use --verbose full to print detail for every request (short smoke/debug runs).");
        output.WriteLine("  Verbose output includes session index, payload variant, bodies, and HTTP status.");
        output.WriteLine();
        output.WriteLine("Supported HTTP methods:");
        output.WriteLine("  GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS");
        output.WriteLine();
        output.WriteLine("  POST, PUT, and PATCH send the payload file as the request body.");
        output.WriteLine("  Other methods require a payload file but do not attach a body.");
        output.WriteLine();
        output.WriteLine("Interval formats:");
        output.WriteLine("  Seconds: 1s, 2.5s");
        output.WriteLine("  Milliseconds: 500ms, 250ms");
        output.WriteLine("  Time span: 00:00:01, 00:00:00.500");
        output.WriteLine("  --timeout uses the same duration formats (default: 100s).");
        output.WriteLine("  --cycle-interval uses the same duration formats (default: 0s).");
        output.WriteLine();
        output.WriteLine("Authentication:");
        output.WriteLine("  Use --auth to send an Authorization header with each request.");
        output.WriteLine("  Pass the full header value (for example: Bearer <token>).");
        output.WriteLine();
        output.WriteLine("Stopping:");
        output.WriteLine("  Press Ctrl+C to stop scheduling new waves; the in-flight wave completes before exit.");
        output.WriteLine();
        output.WriteLine("Exit codes:");
        output.WriteLine("  0  All requests completed successfully");
        output.WriteLine("  1  One or more requests failed, or arguments were invalid");
        output.WriteLine("  2  The session was cancelled (for example, via Ctrl+C)");
    }

    private sealed class ExtendedHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _defaultHelp;

        public ExtendedHelpAction(HelpAction defaultHelp)
        {
            _defaultHelp = defaultHelp;
        }

        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult)
        {
            var result = _defaultHelp.Invoke(parseResult);
            WriteUsageGuide(Console.Out);
            return result;
        }
    }

    private sealed class AppVersionAction : SynchronousCommandLineAction
    {
        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult)
        {
            Console.WriteLine(GetAppVersion());
            return 0;
        }
    }
}
