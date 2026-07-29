namespace Stressor.Core;

/// <summary>Sends a single HTTP request and records the outcome.</summary>
public interface IHttpStressTestClient
{
    /// <summary>Sends one request using the given options and payload body.</summary>
    Task<RequestOutcome> SendAsync(
        StressTestOptions options,
        string payload,
        int cycleNumber,
        int requestNumber,
        CancellationToken cancellationToken = default);
}
