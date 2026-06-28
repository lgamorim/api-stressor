namespace Stressor.Core;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStressorCore(this IServiceCollection services)
    {
        services.AddSingleton<IJsonPayloadReader, JsonPayloadReader>();
        services.AddSingleton<IHttpStressTestClient, HttpStressTestClient>();
        services.AddSingleton<IStressTestRunner, StressTestRunner>();
        services.AddSingleton<IConsoleSessionReporter, ConsoleSessionReporter>();
        services.AddSingleton<IDelayProvider, DelayProvider>();
        return services;
    }
}
