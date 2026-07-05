namespace Stressor.Core.Tests;

internal static class TestCancellation
{
    internal static CancellationToken Token => TestContext.Current.CancellationToken;
}
