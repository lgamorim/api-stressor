namespace Stressor.Core;

using System.Globalization;

/// <summary>Formats session progress snapshots for console output.</summary>
public static class SessionProgressFormatter
{
    /// <summary>Formats a progress line for cycle-limited or duration-limited sessions.</summary>
    public static string Format(SessionProgressSnapshot snapshot)
    {
        var prefix = snapshot.TotalDuration is not null
            ? $"[{FormatDuration(snapshot.Elapsed!.Value)}/{FormatDuration(snapshot.TotalDuration.Value)}]"
            : $"[{snapshot.CompletedRequests.ToString(CultureInfo.InvariantCulture)}/{snapshot.TotalRequests!.Value.ToString(CultureInfo.InvariantCulture)}]";

        var cyclePart = snapshot.TotalDuration is not null
            ? $"  cycle {snapshot.CycleNumber.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;

        var line = $"{prefix}{cyclePart}  OK {snapshot.Succeeded.ToString(CultureInfo.InvariantCulture)}  Fail {snapshot.Failed.ToString(CultureInfo.InvariantCulture)}";

        if (snapshot.Cancelled > 0)
        {
            line += $"  Cancel {snapshot.Cancelled.ToString(CultureInfo.InvariantCulture)}";
        }

        return line;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            var minutes = (int)duration.TotalMinutes;
            var seconds = duration.Seconds;
            return $"{minutes.ToString(CultureInfo.InvariantCulture)}m{seconds.ToString("D2", CultureInfo.InvariantCulture)}s";
        }

        if (duration.TotalSeconds >= 1 && duration.Milliseconds == 0)
        {
            return $"{((int)duration.TotalSeconds).ToString(CultureInfo.InvariantCulture)}s";
        }

        return $"{duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}ms";
    }
}
