namespace Stressor.Core.Tests;

using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

public class HttpStressTestClientTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task SendAsync_BodyBearingMethods_SendsJsonBody(string methodName)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(new HttpMethod(methodName));

        var outcome = await client.SendAsync(options, "{\"a\":1}", 1, 1, TestCancellation.Token);

        Assert.True(outcome.IsSuccess);
        var request = Assert.Single(handler.Requests);
        Assert.NotNull(request.Content);
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"a\":1}", Assert.Single(handler.RequestBodies));
        Assert.Equal(options.Url, request.RequestUri);
        Assert.Equal(methodName, request.Method.Method);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task SendAsync_NonBodyMethods_DoesNotAttachBody(string methodName)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(new HttpMethod(methodName));

        await client.SendAsync(options, "{\"a\":1}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Content);
    }

    [Fact]
    public async Task SendAsync_SuccessResponse_RecordsLatencyAndStatusCode()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(200, outcome.StatusCode);
        Assert.True(outcome.Latency >= TimeSpan.Zero);
        Assert.False(outcome.IsCancelled);
    }

    [Fact]
    public async Task SendAsync_ErrorStatusCode_IncludesResponseBodySummary()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"detail":"Invalid order"}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(400, outcome.StatusCode);
        Assert.Contains("HTTP 400 Bad Request", outcome.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("detail: Invalid order", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ErrorStatusCode_MarksFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(500, outcome.StatusCode);
        Assert.NotNull(outcome.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_HttpRequestException_RecordsFailureMessage()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("network down", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_HttpRequestException_UnwrapsInnerExceptionMessage()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException(
            "No connection could be made",
            new IOException("Connection refused")));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("No connection could be made", outcome.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Connection refused", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_TaskCanceledException_RecordsTimeoutMessage()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException());
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.False(outcome.IsCancelled);
        Assert.Equal("Request timed out.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_CancellationToken_RecordsCancellationOutcome()
    {
        var handler = new StubHttpMessageHandler(_ => throw new OperationCanceledException());
        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, cts.Token);

        Assert.True(outcome.IsCancelled);
    }

    [Fact]
    public async Task SendAsync_AuthProvided_SendsAuthorizationHeader()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Auth = "Bearer secret-token" };

        await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer secret-token", request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task SendAsync_AuthOmitted_DoesNotSendAuthorizationHeader()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_ExceedsRequestTimeout_RecordsTimeoutMessage()
    {
        var handler = new DelayingHttpMessageHandler();
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { RequestTimeout = TimeSpan.FromMilliseconds(50) };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.False(outcome.IsCancelled);
        Assert.Equal("Request timed out.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_CompletesWithinTimeout_ReturnsSuccess()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { RequestTimeout = TimeSpan.FromMilliseconds(50) };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.IsCancelled);
        Assert.Null(outcome.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_SessionCancelledDuringSlowRequest_RecordsCancellationNotTimeout()
    {
        var handler = new DelayingHttpMessageHandler();
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { RequestTimeout = TimeSpan.FromSeconds(30) };
        using var cts = new CancellationTokenSource();

        var sendTask = client.SendAsync(options, "{}", 1, 1, cts.Token);
        cts.Cancel();

        var outcome = await sendTask;

        Assert.True(outcome.IsCancelled);
        Assert.Equal("Request was cancelled.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_Success_VerboseOff_DoesNotCaptureResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":123}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.Null(outcome.ResponseBody);
    }

    [Fact]
    public async Task SendAsync_Success_VerboseFull_CapturesResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":123}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Verbose = VerboseMode.Full };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.Equal("""{"id":123}""", outcome.ResponseBody);
    }

    [Fact]
    public async Task SendAsync_Success_VerboseFailures_DoesNotCaptureResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":123}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Verbose = VerboseMode.Failures };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.Null(outcome.ResponseBody);
    }

    [Fact]
    public async Task SendAsync_Error_VerboseFailures_CapturesResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"detail":"Invalid order"}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Verbose = VerboseMode.Failures };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.Equal("""{"detail":"Invalid order"}""", outcome.ResponseBody);
        Assert.Contains("detail: Invalid order", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_Error_VerboseOff_DoesNotCaptureResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"detail":"Invalid order"}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.Null(outcome.ResponseBody);
        Assert.Contains("detail: Invalid order", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_Success_VerboseFull_LargeBody_TruncatesResponseBody()
    {
        var body = $"{{\"data\":\"{new string('x', BodyTruncator.MaxBodyLength + 100)}\"}}";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Verbose = VerboseMode.Full };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.NotNull(outcome.ResponseBody);
        Assert.Contains("... (truncated,", outcome.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_NetworkError_VerboseFailures_DoesNotCaptureResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Verbose = VerboseMode.Failures };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.Null(outcome.ResponseBody);
    }

    [Fact]
    public async Task SendAsync_EmptyBody_VerboseFull_ReturnsNullResponseBody()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Verbose = VerboseMode.Full };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.Null(outcome.ResponseBody);
    }

    private static HttpStressTestClient CreateClient(StubHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("stressor")
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return new HttpStressTestClient(provider.GetRequiredService<IHttpClientFactory>());
    }

    private static HttpStressTestClient CreateClient(DelayingHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("stressor")
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return new HttpStressTestClient(provider.GetRequiredService<IHttpClientFactory>());
    }

    private sealed class DelayingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static StressTestOptions CreateOptions(HttpMethod method) =>
        new(new Uri("https://example.com/api"), "payload.json", method, 1, TimeSpan.FromSeconds(1), 1);
}
