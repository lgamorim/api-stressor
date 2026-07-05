namespace Stressor.Core.Tests;

using System.Net;
using System.Text;

public class RequestFailureFormatterTests
{
    [Fact]
    public void SummarizeBody_ProblemJson_ExtractsDetail()
    {
        const string body = """{"type":"about:blank","title":"Validation failed","status":400,"detail":"Email is required"}""";

        var summary = RequestFailureFormatter.SummarizeBody(body, "application/problem+json");

        Assert.Equal("detail: Email is required", summary);
    }

    [Fact]
    public void SummarizeBody_SimpleJsonMessage_ExtractsMessage()
    {
        const string body = """{"message":"Invalid payload"}""";

        var summary = RequestFailureFormatter.SummarizeBody(body, "application/json");

        Assert.Equal("message: Invalid payload", summary);
    }

    [Fact]
    public void SummarizeBody_LargeJson_TruncatesRawBody()
    {
        var body = $"{{\"data\":\"{new string('x', RequestFailureFormatter.MaxBodyLength + 100)}\"}}";

        var summary = RequestFailureFormatter.SummarizeBody(body, "application/json");

        Assert.NotNull(summary);
        Assert.Contains("... (truncated,", summary, StringComparison.Ordinal);
        Assert.True(summary.Length < body.Length);
    }

    [Fact]
    public void SummarizeBody_Html_StripsTagsAndTruncates()
    {
        var body = "<html><body><h1>Error</h1><p>Something went wrong</p></body></html>";

        var summary = RequestFailureFormatter.SummarizeBody(body, "text/html");

        Assert.Equal("HTML: Error Something went wrong", summary);
    }

    [Fact]
    public void SummarizeBody_LargeHtml_TruncatesText()
    {
        var body = $"<html><body>{new string('x', RequestFailureFormatter.MaxBodyLength + 50)}</body></html>";

        var summary = RequestFailureFormatter.SummarizeBody(body, "text/html");

        Assert.StartsWith("HTML: ", summary, StringComparison.Ordinal);
        Assert.Contains("... (truncated,", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatException_UnwrapsInnerExceptions()
    {
        var inner = new IOException("Connection refused");
        var outer = new HttpRequestException("No connection could be made", inner);

        var message = RequestFailureFormatter.FormatException(outer);

        Assert.Equal("No connection could be made -> Connection refused", message);
    }

    [Fact]
    public void FormatException_HttpRequestExceptionWithStatusCode_IncludesStatus()
    {
        var exception = new HttpRequestException("Bad gateway", null, HttpStatusCode.BadGateway);

        var message = RequestFailureFormatter.FormatException(exception);

        Assert.Equal("HTTP 502 BadGateway — Bad gateway", message);
    }

    [Fact]
    public void FormatTimeout_ReturnsExpectedMessage()
    {
        Assert.Equal("Request timed out.", RequestFailureFormatter.FormatTimeout());
    }

    [Fact]
    public async Task FormatHttpErrorAsync_IncludesResponseBodySummary()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"detail":"Name is required"}""", Encoding.UTF8, "application/json")
        };

        var message = await RequestFailureFormatter.FormatHttpErrorAsync(response, TestCancellation.Token);

        Assert.Equal("HTTP 400 Bad Request — detail: Name is required", message);
    }
}
