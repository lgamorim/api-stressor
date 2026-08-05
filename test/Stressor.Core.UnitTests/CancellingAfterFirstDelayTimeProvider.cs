namespace Stressor.Core.UnitTests;

public sealed class CancellingAfterFirstDelayTimeProvider : TimeProvider
{
    private readonly CancellationTokenSource _cancellationTokenSource;

    public CancellingAfterFirstDelayTimeProvider(CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
    }

    public List<TimeSpan> Delays { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Delays.Add(dueTime);
        callback(state);

        if (Delays.Count == 1)
        {
            _cancellationTokenSource.Cancel();
        }

        return new NoOpTimer();
    }

    private sealed class NoOpTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
