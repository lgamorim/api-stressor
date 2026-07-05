namespace Stressor.Core;

public interface IHttpStressTestClient
{
    Task<RequestOutcome> SendAsync(
        StressTestOptions options,
        string payload,
        int cycleNumber,
        int requestNumber,
        CancellationToken cancellationToken = default);
}
