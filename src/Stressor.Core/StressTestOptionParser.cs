namespace Stressor.Core;

using System.Globalization;

/// <summary>Parses merged configuration values into <see cref="StressTestOptions"/>.</summary>
public static class StressTestOptionParser
{
    /// <summary>Builds validated options from merged configuration values.</summary>
    public static (StressTestOptions? Options, IReadOnlyList<string> Errors) TryCreateOptions(StressTestConfigurationValues values)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(values.Url))
        {
            errors.Add("URL is required.");
        }

        if (string.IsNullOrWhiteSpace(values.Payload))
        {
            errors.Add("Payload file path is required.");
        }

        if (values.Requests is null)
        {
            errors.Add("Requests per interval is required.");
        }

        if (string.IsNullOrWhiteSpace(values.Interval))
        {
            errors.Add("Interval is required.");
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        if (!Uri.TryCreate(values.Url, UriKind.Absolute, out var uri))
        {
            errors.Add("URL must be absolute.");
            return (null, errors);
        }

        if (!TryParseLoadMode(values.Load, out var loadMode))
        {
            errors.Add("Load must be gentle-pacing, fixed-rate, or batch.");
            return (null, errors);
        }

        if (!TryParseInterval(values.Interval!, allowZero: loadMode == LoadMode.Batch, out var intervalSpan))
        {
            errors.Add("Interval must be a valid duration (e.g. 1s, 500ms, 00:00:01).");
            return (null, errors);
        }

        if (!TryParseInterval(values.Timeout, allowZero: false, out var timeoutSpan))
        {
            errors.Add("Timeout must be a valid duration (e.g. 30s, 500ms, 00:01:40).");
            return (null, errors);
        }

        if (!TryParseInterval(values.CycleInterval, allowZero: true, out var cycleIntervalSpan))
        {
            errors.Add("Cycle interval must be a valid duration (e.g. 30s, 500ms, 00:00:30).");
            return (null, errors);
        }

        if (!TryParseVerboseMode(values.Verbose, out var verboseMode))
        {
            errors.Add("Verbose must be failures or full.");
            return (null, errors);
        }

        var options = new StressTestOptions(
            uri,
            values.Payload!,
            new HttpMethod(values.Method),
            values.Requests!.Value,
            intervalSpan,
            values.Cycles,
            values.Auth,
            verboseMode,
            loadMode,
            values.Batch)
        {
            RequestTimeout = timeoutSpan,
            CycleInterval = cycleIntervalSpan
        };

        var validationErrors = StressTestOptionsValidator.Validate(options);
        if (validationErrors.Count > 0)
        {
            return (null, validationErrors);
        }

        return (options, []);
    }

    /// <summary>Parses a duration string into a <see cref="TimeSpan"/>.</summary>
    public static bool TryParseInterval(string value, bool allowZero, out TimeSpan interval)
    {
        if (TimeSpan.TryParse(value, out interval))
        {
            return allowZero ? interval >= TimeSpan.Zero : interval > TimeSpan.Zero;
        }

        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
        {
            interval = TimeSpan.FromMilliseconds(milliseconds);
            return allowZero ? interval >= TimeSpan.Zero : interval > TimeSpan.Zero;
        }

        if (value.EndsWith('s') && !value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            interval = TimeSpan.FromSeconds(seconds);
            return allowZero ? interval >= TimeSpan.Zero : interval > TimeSpan.Zero;
        }

        interval = default;
        return false;
    }

    /// <summary>Parses a duration string into a <see cref="TimeSpan"/>.</summary>
    public static bool TryParseInterval(string value, out TimeSpan interval) =>
        TryParseInterval(value, allowZero: false, out interval);

    /// <summary>Parses a load-mode string.</summary>
    public static bool TryParseLoadMode(string value, out LoadMode loadMode)
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

    /// <summary>Parses a verbose-mode string.</summary>
    public static bool TryParseVerboseMode(string? value, out VerboseMode verboseMode)
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
