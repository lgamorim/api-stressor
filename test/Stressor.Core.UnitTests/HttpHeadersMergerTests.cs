namespace Stressor.Core.UnitTests;

public class HttpHeadersMergerTests
{
    [Fact]
    public void Should_ReturnEmpty_When_NoLayers()
    {
        var merged = HttpHeadersMerger.Merge();

        Assert.Empty(merged);
    }

    [Fact]
    public void Should_MergeLayers_When_MultipleProvided()
    {
        var merged = HttpHeadersMerger.Merge(
            new Dictionary<string, string> { ["X-A"] = "1" },
            new Dictionary<string, string> { ["X-B"] = "2" });

        Assert.Equal("1", merged["X-A"]);
        Assert.Equal("2", merged["X-B"]);
    }

    [Fact]
    public void Should_OverrideEarlierLayer_When_SameNameCaseInsensitive()
    {
        var merged = HttpHeadersMerger.Merge(
            new Dictionary<string, string> { ["X-Api-Key"] = "old" },
            new Dictionary<string, string> { ["x-api-key"] = "new" });

        Assert.Single(merged);
        Assert.Equal("new", merged["X-Api-Key"]);
    }
}
