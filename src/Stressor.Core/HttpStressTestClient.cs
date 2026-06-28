namespace Stressor.Core;

using System.Diagnostics;
using System.Text;

public sealed class HttpStressTestClient : IHttpStressTestClient
{
    private const string ClientName = "stressor";
    private readonly IHttpClientFactory httpClientFactory;

    public HttpStressTestClient(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    public async Task<RequestOutcome> SendAsync(
        StressTestOptions options,
        string payload,
        int cycleNumber,
        int requestNumber,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = CreateRequest(options, payload);
            var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new RequestOutcome(
                cycleNumber,
                requestNumber,
                response.IsSuccessStatusCode,
                false,
                (int)response.StatusCode,
                stopwatch.Elapsed,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new RequestOutcome(
                cycleNumber,
                requestNumber,
                false,
                true,
                null,
                stopwatch.Elapsed,
                "Request was cancelled.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new RequestOutcome(
                cycleNumber,
                requestNumber,
                false,
                false,
                null,
                stopwatch.Elapsed,
                ex.Message);
        }
    }

    internal static HttpRequestMessage CreateRequest(StressTestOptions options, string payload)
    {
        var request = new HttpRequestMessage(options.Method, options.Url);

        if (HttpMethodSupportsBody(options.Method))
        {
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        if (!string.IsNullOrWhiteSpace(options.Auth))
        {
            request.Headers.TryAddWithoutValidation("Authorization", options.Auth);
        }

        return request;
    }

    internal static bool HttpMethodSupportsBody(HttpMethod method) =>
        HttpMethod.Post.Method.Equals(method.Method, StringComparison.OrdinalIgnoreCase)
        || HttpMethod.Put.Method.Equals(method.Method, StringComparison.OrdinalIgnoreCase)
        || HttpMethod.Patch.Method.Equals(method.Method, StringComparison.OrdinalIgnoreCase);
}
