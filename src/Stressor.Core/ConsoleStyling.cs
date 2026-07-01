namespace Stressor.Core;

internal static class ConsoleStyling
{
    private const string Red = "\x1b[31m";
    private const string Green = "\x1b[32m";
    private const string Reset = "\x1b[0m";

    internal static string FormatErrorPrefix(TextWriter output) =>
        SupportsColor(output) ? $"{Red}Fail:{Reset} " : "Fail: ";

    internal static string FormatSuccessPrefix(TextWriter output) =>
        SupportsColor(output) ? $"{Green}OK:{Reset} " : "OK: ";

    private static bool SupportsColor(TextWriter output) =>
        !Console.IsOutputRedirected && ReferenceEquals(output, Console.Out);
}
