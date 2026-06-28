namespace Stressor.Core;

public sealed class DelayProvider : IDelayProvider
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        Task.Delay(delay, cancellationToken);
}
