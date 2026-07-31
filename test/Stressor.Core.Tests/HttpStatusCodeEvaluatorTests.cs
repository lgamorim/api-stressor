namespace Stressor.Core.Tests;

public class HttpStatusCodeEvaluatorTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(299)]
    public void Should_ReturnTrue_When_DefaultAndTwoHundredSeries(int statusCode)
    {
        Assert.True(HttpStatusCodeEvaluator.IsSuccess(statusCode, new HashSet<int>()));
    }

    [Theory]
    [InlineData(199)]
    [InlineData(300)]
    [InlineData(404)]
    [InlineData(500)]
    public void Should_ReturnFalse_When_DefaultAndNotTwoHundredSeries(int statusCode)
    {
        Assert.False(HttpStatusCodeEvaluator.IsSuccess(statusCode, new HashSet<int>()));
    }

    [Fact]
    public void Should_ReturnTrue_When_StatusInConfiguredSet()
    {
        var expected = new HashSet<int> { 200, 201 };

        Assert.True(HttpStatusCodeEvaluator.IsSuccess(201, expected));
    }

    [Fact]
    public void Should_ReturnFalse_When_StatusNotInConfiguredSet()
    {
        var expected = new HashSet<int> { 200 };

        Assert.False(HttpStatusCodeEvaluator.IsSuccess(201, expected));
    }

    [Fact]
    public void Should_ReturnTrue_When_FourHundredConfiguredAsExpected()
    {
        var expected = new HashSet<int> { 404 };

        Assert.True(HttpStatusCodeEvaluator.IsSuccess(404, expected));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(599)]
    public void Should_EvaluateBoundaryCodes_When_Configured(int statusCode)
    {
        var expected = new HashSet<int> { statusCode };

        Assert.True(HttpStatusCodeEvaluator.IsSuccess(statusCode, expected));
    }
}
