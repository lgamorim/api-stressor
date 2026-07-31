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
    public async Task Should_SendJsonBody_When_BodyBearingMethods(string methodName)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(new HttpMethod(methodName));

        var outcome = await client.SendAsync(options, "{\"a\":1}", 1, 1, TestCancellation.Token);

        Assert.True(outcome.IsSuccess);
        var request = Assert.Single(handler.Requests);
        Assert.NotNull(request.Content);
        Assert.NotNull(request.Content.Headers.ContentType);
        Assert.Equal("application/json", request.Content.Headers.ContentType.MediaType);
        Assert.Equal("{\"a\":1}", Assert.Single(handler.RequestBodies));
        Assert.Equal(options.Url, request.RequestUri);
        Assert.Equal(methodName, request.Method.Method);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task Should_NotAttachBody_When_NonBodyMethods(string methodName)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(new HttpMethod(methodName));

        await client.SendAsync(options, "{\"a\":1}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Content);
    }

    [Fact]
    public async Task Should_RecordLatencyAndStatusCode_When_SuccessResponse()
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
    public async Task Should_IncludeResponseBodySummary_When_ErrorStatusCode()
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
    public async Task Should_MarkFailure_When_ErrorStatusCode()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(500, outcome.StatusCode);
        Assert.NotNull(outcome.ErrorMessage);
    }

    [Fact]
    public async Task Should_RecordFailureMessage_When_HttpRequestException()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("network down", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_UnwrapInnerExceptionMessage_When_HttpRequestException()
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
    public async Task Should_RecordTimeoutMessage_When_TaskCanceledException()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException());
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.False(outcome.IsCancelled);
        Assert.Equal("Request timed out.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task Should_RecordCancellationOutcome_When_CancellationToken()
    {
        var handler = new StubHttpMessageHandler(_ => throw new OperationCanceledException());
        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, cts.Token);

        Assert.True(outcome.IsCancelled);
    }

    [Fact]
    public async Task Should_TreatTwoHundredAsSuccess_When_NoExpectedStatusConfigured()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(200, outcome.StatusCode);
    }

    [Fact]
    public async Task Should_TreatConfiguredCodeAsSuccess_When_StatusMatches()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with
        {
            ExpectedStatusCodes = new HashSet<int> { 201 }
        };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(201, outcome.StatusCode);
    }

    [Fact]
    public async Task Should_TreatResponseAsFailure_When_StatusNotInExpectedSet()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with
        {
            ExpectedStatusCodes = new HashSet<int> { 200 }
        };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(201, outcome.StatusCode);
        Assert.Contains("expected 200", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_TreatFourHundredAsSuccess_When_ConfiguredAsExpected()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with
        {
            ExpectedStatusCodes = new HashSet<int> { 404 }
        };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(404, outcome.StatusCode);
    }

    [Fact]
    public async Task Should_IncludeExpectedCodesInError_When_StatusMismatch()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with
        {
            ExpectedStatusCodes = new HashSet<int> { 200, 201 }
        };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("expected 200 or 201", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_SendCustomHeader_When_HeadersProvided()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with
        {
            Headers = new Dictionary<string, string> { ["X-Api-Key"] = "abc123" }
        };

        await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.True(request.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("abc123", Assert.Single(values));
    }

    [Fact]
    public async Task Should_OverrideContentType_When_HeaderProvided()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with
        {
            Headers = new Dictionary<string, string> { ["Content-Type"] = "text/plain" }
        };

        await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.NotNull(request.Content?.Headers.ContentType);
        Assert.Equal("text/plain", request.Content.Headers.ContentType.MediaType);
    }

    [Fact]
    public async Task Should_ApplyAuthAfterHeaders_When_BothProvided()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer from-headers" },
            Auth = "Bearer from-auth"
        };

        await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer from-auth", request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task Should_SendAuthorizationHeader_When_AuthProvided()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Auth = "Bearer secret-token" };

        await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer secret-token", request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task Should_NotSendAuthorizationHeader_When_AuthOmitted()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1, TestCancellation.Token);

        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task Should_RecordTimeoutMessage_When_ExceedRequestTimeout()
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
    public async Task Should_ReturnSuccess_When_CompleteWithinTimeout()
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
    public async Task Should_RecordCancellationNotTimeout_When_SessionCancelledDuringSlowRequest()
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
    public async Task Should_NotCaptureResponseBody_When_Success_VerboseOff()
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
    public async Task Should_CaptureResponseBody_When_Success_VerboseFull()
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
    public async Task Should_NotCaptureResponseBody_When_Success_VerboseFailures()
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
    public async Task Should_CaptureResponseBody_When_Error_VerboseFailures()
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
    public async Task Should_NotCaptureResponseBody_When_Error_VerboseOff()
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
    public async Task Should_TruncateResponseBody_When_Success_VerboseFull_LargeBody()
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
    public async Task Should_NotCaptureResponseBody_When_NetworkError_VerboseFailures()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);
        var options = CreateOptions(HttpMethod.Post) with { Verbose = VerboseMode.Failures };

        var outcome = await client.SendAsync(options, "{}", 1, 1, TestCancellation.Token);

        Assert.Null(outcome.ResponseBody);
    }

    [Fact]
    public async Task Should_ReturnNullResponseBody_When_EmptyBody_VerboseFull()
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
        private static readonly TaskCompletionSource NeverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await NeverCompletes.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static StressTestOptions CreateOptions(HttpMethod method) =>
        new(new Uri("https://example.com/api"), "payload.json", method, 1, TimeSpan.FromSeconds(1), 1);
}
