namespace Stressor.Core;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for stressor core services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers core stressor services with the service collection.</summary>
    public static IServiceCollection AddStressorCore(this IServiceCollection services)
    {
        services.AddSingleton<IJsonPayloadReader, JsonPayloadReader>();
        services.AddSingleton<IStressTestScenarioReader, JsonStressTestScenarioReader>();
        services.AddSingleton<IHttpStressTestClient, HttpStressTestClient>();
        services.AddSingleton<IStressTestRunner, StressTestRunner>();
        services.AddSingleton<IConsoleSessionReporter, ConsoleSessionReporter>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
