namespace Stressor.Core;

public interface IDelayProvider
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
