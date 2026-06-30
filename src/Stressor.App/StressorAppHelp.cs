namespace Stressor.App;

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

public static class StressorAppHelp
{
    internal static void Configure(RootCommand rootCommand)
    {
        foreach (var option in rootCommand.Options)
        {
            if (option is HelpOption helpOption && helpOption.Action is HelpAction defaultHelp)
            {
                helpOption.Action = new ExtendedHelpAction(defaultHelp);
                break;
            }
        }
    }

    public static void WriteUsageGuide(TextWriter output)
    {
        output.WriteLine();
        output.WriteLine("Examples:");
        output.WriteLine("  Stressor.App --url https://api.example.com/orders --payload ./payload.json \\");
        output.WriteLine("    --requests 10 --interval 1s --cycles 60");
        output.WriteLine();
        output.WriteLine("  Stressor.App -u https://api.example.com/orders -p ./payloads.json -m POST \\");
        output.WriteLine("    -r 10 -i 1s -c 60 -a \"Bearer your-token-here\"");
        output.WriteLine();
        output.WriteLine("Payload file:");
        output.WriteLine("  Single body: any JSON value sent unchanged on every request.");
        output.WriteLine("  Multi-payload: root object with only a \"payloads\" array; items rotate");
        output.WriteLine("    per request within each cycle, wrapping when requests exceed payload count.");
        output.WriteLine();
        output.WriteLine("Load:");
        output.WriteLine("  Each cycle sends --requests calls spread evenly across --interval.");
        output.WriteLine("  Total requests in a session = requests x cycles.");
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
        output.WriteLine();
        output.WriteLine("Authentication:");
        output.WriteLine("  Use --auth to send an Authorization header with each request.");
        output.WriteLine("  Pass the full header value (for example: Bearer <token>).");
        output.WriteLine();
        output.WriteLine("Stopping:");
        output.WriteLine("  Press Ctrl+C to stop after the current request completes.");
        output.WriteLine();
        output.WriteLine("Exit codes:");
        output.WriteLine("  0  All requests completed successfully");
        output.WriteLine("  1  One or more requests failed, or arguments were invalid");
        output.WriteLine("  2  The session was cancelled (for example, via Ctrl+C)");
    }

    private sealed class ExtendedHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction defaultHelp;

        public ExtendedHelpAction(HelpAction defaultHelp)
        {
            this.defaultHelp = defaultHelp;
        }

        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult)
        {
            var result = defaultHelp.Invoke(parseResult);
            WriteUsageGuide(Console.Out);
            return result;
        }
    }
}
