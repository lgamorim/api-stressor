namespace Stressor.Core.Tests;

using System.Net;
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

        var outcome = await client.SendAsync(options, "{\"a\":1}", 1, 1);

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

        await client.SendAsync(options, "{\"a\":1}", 1, 1);

        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Content);
    }

    [Fact]
    public async Task SendAsync_SuccessResponse_RecordsLatencyAndStatusCode()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(200, outcome.StatusCode);
        Assert.True(outcome.Latency >= TimeSpan.Zero);
        Assert.False(outcome.IsCancelled);
    }

    [Fact]
    public async Task SendAsync_ErrorStatusCode_MarksFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(500, outcome.StatusCode);
        Assert.NotNull(outcome.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_HttpRequestException_RecordsFailureMessage()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("network down", outcome.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_TaskCanceledException_RecordsFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException());
        var client = CreateClient(handler);

        var outcome = await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1);

        Assert.False(outcome.IsSuccess);
        Assert.False(outcome.IsCancelled);
        Assert.NotNull(outcome.ErrorMessage);
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

        await client.SendAsync(options, "{}", 1, 1);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer secret-token", request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task SendAsync_AuthOmitted_DoesNotSendAuthorizationHeader()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.SendAsync(CreateOptions(HttpMethod.Post), "{}", 1, 1);

        var request = Assert.Single(handler.Requests);
        Assert.Null(request.Headers.Authorization);
    }

    private static HttpStressTestClient CreateClient(StubHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("stressor").ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return new HttpStressTestClient(provider.GetRequiredService<IHttpClientFactory>());
    }

    private static StressTestOptions CreateOptions(HttpMethod method) =>
        new(new Uri("https://example.com/api"), "payload.json", method, 1, TimeSpan.FromSeconds(1), 1);
}
