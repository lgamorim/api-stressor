namespace Stressor.Core.UnitTests;

public class StressTestConfigurationMergerTests
{
    [Fact]
    public void Should_MergeConfigReportPath_When_DocumentProvidesReport()
    {
        var document = new StressTestScenarioDocument
        {
            Report = "./results/report.json"
        };

        var configPath = Path.Combine(Path.GetTempPath(), "scenarios", "scenario.json");
        var merged = StressTestConfigurationMerger.Merge(
            document,
            configPath,
            new StressTestCliOverrides());

        Assert.Equal(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "scenarios", "results", "report.json")),
            merged.Report);
    }

    [Fact]
    public void Should_MergeConfigExpectStatus_When_DocumentProvidesExpectStatus()
    {
        var document = new StressTestScenarioDocument
        {
            ExpectStatus = [200, 201]
        };

        var merged = StressTestConfigurationMerger.Merge(document, null, new StressTestCliOverrides());

        Assert.Equal([200, 201], merged.ExpectStatus.OrderBy(code => code));
    }

    [Fact]
    public void Should_MergeConfigHeaders_When_DocumentProvidesHeaders()
    {
        var document = new StressTestScenarioDocument
        {
            Headers = new Dictionary<string, string> { ["X-Tenant-Id"] = "acme" }
        };

        var merged = StressTestConfigurationMerger.Merge(document, null, new StressTestCliOverrides());

        Assert.Equal("acme", merged.Headers["X-Tenant-Id"]);
    }

    [Fact]
    public void Should_UseConfigValues_When_CliOmitted()
    {
        var document = new StressTestScenarioDocument
        {
            Url = "https://example.com",
            Payload = "./payload.json",
            Method = "PUT",
            Requests = 10,
            Interval = "500ms",
            Cycles = 60,
            Auth = "Bearer token",
            Verbose = "failures",
            Load = "batch",
            Batch = 5,
            Timeout = "30s",
            CycleInterval = "10s"
        };

        var configPath = Path.Combine(Path.GetTempPath(), "configs", "scenario.json");
        var merged = StressTestConfigurationMerger.Merge(document, configPath, new StressTestCliOverrides());

        Assert.Equal("https://example.com", merged.Url);
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configPath)!, "payload.json")), merged.Payload);
        Assert.Equal("PUT", merged.Method);
        Assert.Equal(10, merged.Requests);
        Assert.Equal("500ms", merged.Interval);
        Assert.Equal(60, merged.Cycles);
        Assert.Equal("Bearer token", merged.Auth);
        Assert.Equal("failures", merged.Verbose);
        Assert.Equal("batch", merged.Load);
        Assert.Equal(5, merged.Batch);
        Assert.Equal("30s", merged.Timeout);
        Assert.Equal("10s", merged.CycleInterval);
    }

    [Fact]
    public void Should_OverrideConfig_When_CliExplicit()
    {
        var document = new StressTestScenarioDocument
        {
            Url = "https://example.com",
            Payload = "./payload.json",
            Requests = 10,
            Interval = "1s",
            Cycles = 60
        };

        var cli = new StressTestCliOverrides
        {
            SpecifiedOptions = new HashSet<string> { StressTestConfigurationOptionNames.Cycles },
            Cycles = 10
        };

        var configPath = Path.Combine(Path.GetTempPath(), "configs", "scenario.json");
        var merged = StressTestConfigurationMerger.Merge(document, configPath, cli);

        Assert.Equal(10, merged.Cycles);
        Assert.Equal("https://example.com", merged.Url);
    }

    [Fact]
    public void Should_ResolvePayloadRelativeToConfigDirectory()
    {
        var document = new StressTestScenarioDocument
        {
            Payload = "./data/payload.json"
        };

        var configPath = Path.Combine(Path.GetTempPath(), "scenarios", "scenario.json");
        var merged = StressTestConfigurationMerger.Merge(document, configPath, new StressTestCliOverrides());

        Assert.Equal(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "scenarios", "data", "payload.json")),
            merged.Payload);
    }

    [Fact]
    public void Should_MergeConfigDuration_When_DocumentProvidesDuration()
    {
        var document = new StressTestScenarioDocument
        {
            Duration = "5m"
        };

        var merged = StressTestConfigurationMerger.Merge(document, null, new StressTestCliOverrides());

        Assert.Equal("5m", merged.Duration);
        Assert.True(merged.DurationSpecified);
        Assert.False(merged.CyclesSpecified);
    }

    [Fact]
    public void Should_OverrideConfigDuration_When_CliExplicit()
    {
        var document = new StressTestScenarioDocument
        {
            Duration = "5m"
        };

        var cli = new StressTestCliOverrides
        {
            SpecifiedOptions = new HashSet<string> { StressTestConfigurationOptionNames.Duration },
            Duration = "10m"
        };

        var merged = StressTestConfigurationMerger.Merge(document, null, cli);

        Assert.Equal("10m", merged.Duration);
        Assert.True(merged.DurationSpecified);
    }

    [Fact]
    public void Should_MergeConfigProgress_When_DocumentProvidesProgress()
    {
        var document = new StressTestScenarioDocument { Progress = true };

        var merged = StressTestConfigurationMerger.Merge(document, null, new StressTestCliOverrides());

        Assert.True(merged.Progress);
        Assert.True(merged.ProgressSpecified);
    }

    [Fact]
    public void Should_OverrideConfigProgress_When_CliExplicit()
    {
        var document = new StressTestScenarioDocument { Progress = false };

        var cli = new StressTestCliOverrides
        {
            SpecifiedOptions = new HashSet<string> { StressTestConfigurationOptionNames.Progress },
            Progress = true
        };

        var merged = StressTestConfigurationMerger.Merge(document, null, cli);

        Assert.True(merged.Progress);
        Assert.True(merged.ProgressSpecified);
    }

    [Fact]
    public void Should_MergeConfigDryRun_When_DocumentProvidesDryRun()
    {
        var document = new StressTestScenarioDocument { DryRun = true };

        var merged = StressTestConfigurationMerger.Merge(document, null, new StressTestCliOverrides());

        Assert.True(merged.DryRun);
        Assert.True(merged.DryRunSpecified);
    }

    [Fact]
    public void Should_OverrideConfigDryRun_When_CliExplicit()
    {
        var document = new StressTestScenarioDocument { DryRun = false };

        var cli = new StressTestCliOverrides
        {
            SpecifiedOptions = new HashSet<string> { StressTestConfigurationOptionNames.DryRun },
            DryRun = true
        };

        var merged = StressTestConfigurationMerger.Merge(document, null, cli);

        Assert.True(merged.DryRun);
        Assert.True(merged.DryRunSpecified);
    }
}
