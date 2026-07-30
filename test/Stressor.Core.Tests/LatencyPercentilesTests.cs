namespace Stressor.Core.Tests;

public class LatencyPercentilesTests
{
    [Fact]
    public void Should_ReturnNull_When_Empty()
    {
        Assert.Null(LatencyPercentiles.GetPercentile([], 50));
        Assert.Null(LatencyPercentiles.GetPercentile([], 95));
        Assert.Null(LatencyPercentiles.GetPercentile([], 99));
    }

    [Fact]
    public void Should_ReturnSameValue_When_SingleSample()
    {
        var latencies = new[] { TimeSpan.FromMilliseconds(42) };

        Assert.Equal(TimeSpan.FromMilliseconds(42), LatencyPercentiles.GetPercentile(latencies, 50));
        Assert.Equal(TimeSpan.FromMilliseconds(42), LatencyPercentiles.GetPercentile(latencies, 95));
        Assert.Equal(TimeSpan.FromMilliseconds(42), LatencyPercentiles.GetPercentile(latencies, 99));
    }

    [Fact]
    public void Should_ReturnMedianRank_When_OddCount()
    {
        var latencies = new[]
        {
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(70)
        };

        Assert.Equal(TimeSpan.FromMilliseconds(50), LatencyPercentiles.GetPercentile(latencies, 50));
    }

    [Fact]
    public void Should_ReturnNearestRank_When_EvenCount()
    {
        var latencies = new[]
        {
            TimeSpan.FromMilliseconds(40),
            TimeSpan.FromMilliseconds(60)
        };

        Assert.Equal(TimeSpan.FromMilliseconds(40), LatencyPercentiles.GetPercentile(latencies, 50));
    }

    [Fact]
    public void Should_ReturnHighPercentile_When_LargeSample()
    {
        var latencies = Enumerable.Range(1, 100)
            .Select(i => TimeSpan.FromMilliseconds(i))
            .ToArray();

        Assert.Equal(TimeSpan.FromMilliseconds(95), LatencyPercentiles.GetPercentile(latencies, 95));
        Assert.Equal(TimeSpan.FromMilliseconds(99), LatencyPercentiles.GetPercentile(latencies, 99));
    }
}
