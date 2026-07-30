namespace Stressor.Core;

/// <summary>Merges scenario config files with explicit CLI overrides.</summary>
public static class StressTestConfigurationMerger
{
    /// <summary>
    /// Merges a scenario document with CLI overrides. Explicit CLI values win.
    /// Payload paths from the config file are resolved relative to the config file directory.
    /// </summary>
    public static StressTestConfigurationValues Merge(
        StressTestScenarioDocument? document,
        string? configFilePath,
        StressTestCliOverrides cli)
    {
        var values = new StressTestConfigurationValues();

        if (document is not null)
        {
            values = values with
            {
                Url = document.Url,
                Payload = ResolvePayloadPath(document.Payload, configFilePath, fromConfig: true),
                Method = document.Method ?? values.Method,
                Requests = document.Requests,
                Interval = document.Interval,
                Cycles = document.Cycles ?? values.Cycles,
                Auth = document.Auth,
                Verbose = document.Verbose,
                Load = document.Load ?? values.Load,
                Batch = document.Batch ?? values.Batch,
                Timeout = document.Timeout ?? values.Timeout,
                CycleInterval = document.CycleInterval ?? values.CycleInterval
            };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Url) && cli.Url is not null)
        {
            values = values with { Url = cli.Url };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Payload) && cli.Payload is not null)
        {
            values = values with { Payload = ResolvePayloadPath(cli.Payload, configFilePath, fromConfig: false) };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Method) && cli.Method is not null)
        {
            values = values with { Method = cli.Method };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Requests) && cli.Requests is not null)
        {
            values = values with { Requests = cli.Requests };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Interval) && cli.Interval is not null)
        {
            values = values with { Interval = cli.Interval };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Cycles) && cli.Cycles is not null)
        {
            values = values with { Cycles = cli.Cycles.Value };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Auth))
        {
            values = values with { Auth = cli.Auth };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Verbose))
        {
            values = values with { Verbose = cli.Verbose };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Load) && cli.Load is not null)
        {
            values = values with { Load = cli.Load };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Batch) && cli.Batch is not null)
        {
            values = values with { Batch = cli.Batch.Value };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.Timeout) && cli.Timeout is not null)
        {
            values = values with { Timeout = cli.Timeout };
        }

        if (cli.IsSpecified(StressTestConfigurationOptionNames.CycleInterval) && cli.CycleInterval is not null)
        {
            values = values with { CycleInterval = cli.CycleInterval };
        }

        return values;
    }

    internal static string? ResolvePayloadPath(string? payloadPath, string? configFilePath, bool fromConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath))
        {
            return payloadPath;
        }

        if (!fromConfig || Path.IsPathRooted(payloadPath) || string.IsNullOrWhiteSpace(configFilePath))
        {
            return payloadPath;
        }

        var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configFilePath));
        if (string.IsNullOrEmpty(configDirectory))
        {
            return payloadPath;
        }

        return Path.GetFullPath(Path.Combine(configDirectory, payloadPath));
    }
}
