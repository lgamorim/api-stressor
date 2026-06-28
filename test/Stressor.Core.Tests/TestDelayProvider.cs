namespace Stressor.Core.Tests;

public sealed class TestDelayProvider : IDelayProvider
{
    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        Delays.Add(delay);
        return Task.CompletedTask;
    }
}
