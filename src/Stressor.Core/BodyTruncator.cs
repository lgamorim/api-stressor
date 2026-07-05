namespace Stressor.Core;

internal static class BodyTruncator
{
    internal const int MaxBodyLength = 1024;

    internal static string Truncate(string value, string? prefix = null)
    {
        if (value.Length <= MaxBodyLength)
        {
            return prefix is null ? value : prefix + value;
        }

        var truncated = value[..MaxBodyLength];
        var suffix = $"... (truncated, {value.Length} chars total)";
        return prefix is null ? truncated + suffix : prefix + truncated + suffix;
    }
}
