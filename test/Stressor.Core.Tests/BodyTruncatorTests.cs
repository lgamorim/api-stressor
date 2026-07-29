namespace Stressor.Core.Tests;

public class BodyTruncatorTests
{
    [Fact]
    public void Should_ReturnEmpty_When_EmptyInput()
    {
        Assert.Equal(string.Empty, BodyTruncator.Truncate(string.Empty));
    }

    [Fact]
    public void Should_ReturnUnchanged_When_UnderLimit()
    {
        var value = new string('a', BodyTruncator.MaxBodyLength - 1);

        Assert.Equal(value, BodyTruncator.Truncate(value));
    }

    [Fact]
    public void Should_ReturnUnchanged_When_AtLimit()
    {
        var value = new string('a', BodyTruncator.MaxBodyLength);

        Assert.Equal(value, BodyTruncator.Truncate(value));
    }

    [Fact]
    public void Should_AppendTruncationSuffix_When_OverLimit()
    {
        var value = new string('a', BodyTruncator.MaxBodyLength + 100);

        var result = BodyTruncator.Truncate(value);

        Assert.StartsWith(new string('a', BodyTruncator.MaxBodyLength), result, StringComparison.Ordinal);
        Assert.EndsWith($"... (truncated, {value.Length} chars total)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_PrependPrefix_When_WithPrefix()
    {
        var value = "hello";

        Assert.Equal("HTML: hello", BodyTruncator.Truncate(value, prefix: "HTML: "));
    }

    [Fact]
    public void Should_IncludePrefixInResult_When_WithPrefixAndOverLimit()
    {
        var value = new string('x', BodyTruncator.MaxBodyLength + 50);

        var result = BodyTruncator.Truncate(value, prefix: "HTML: ");

        Assert.StartsWith("HTML: ", result, StringComparison.Ordinal);
        Assert.EndsWith($"... (truncated, {value.Length} chars total)", result, StringComparison.Ordinal);
    }
}
