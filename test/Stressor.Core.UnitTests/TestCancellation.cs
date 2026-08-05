namespace Stressor.Core.UnitTests;

internal static class TestCancellation
{
    internal static CancellationToken Token => TestContext.Current.CancellationToken;
}
