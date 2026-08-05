namespace Stressor.App.UnitTests;

internal static class TestCancellation
{
    internal static CancellationToken Token => TestContext.Current.CancellationToken;
}
