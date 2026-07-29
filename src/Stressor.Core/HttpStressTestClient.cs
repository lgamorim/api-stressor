namespace Stressor.Core;

using System.Diagnostics;
using System.Text;

/// <summary>Sends HTTP requests for stress testing via a named <see cref="IHttpClientFactory"/> client.</summary>
public sealed class HttpStressTestClient : IHttpStressTestClient
{
    private const string ClientName = "stressor";
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Creates a client that resolves HTTP clients from the given factory.</summary>
    public HttpStressTestClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
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
            var client = _httpClientFactory.CreateClient(ClientName);
            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestCts.CancelAfter(options.RequestTimeout);
            using var response = await client.SendAsync(request, requestCts.Token).ConfigureAwait(false);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                string? responseBody = null;
                if (options.Verbose == VerboseMode.Full)
                {
                    responseBody = await ReadTruncatedBodyAsync(response, cancellationToken).ConfigureAwait(false);
                }

                return new RequestOutcome(
                    cycleNumber,
                    requestNumber,
                    true,
                    false,
                    (int)response.StatusCode,
                    stopwatch.Elapsed,
                    null,
                    responseBody);
            }

            string errorMessage;
            string? responseBodyOnError = null;

            if (options.Verbose == VerboseMode.Off)
            {
                errorMessage = await RequestFailureFormatter.FormatHttpErrorAsync(response, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var mediaType = response.Content?.Headers.ContentType?.MediaType;
                var body = response.Content is null
                    ? null
                    : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                responseBodyOnError = ToTruncatedBody(body);
                errorMessage = RequestFailureFormatter.FormatHttpError(
                    response.StatusCode,
                    response.ReasonPhrase,
                    body,
                    mediaType);
            }

            return new RequestOutcome(
                cycleNumber,
                requestNumber,
                false,
                false,
                (int)response.StatusCode,
                stopwatch.Elapsed,
                errorMessage,
                responseBodyOnError);
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
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new RequestOutcome(
                cycleNumber,
                requestNumber,
                false,
                false,
                null,
                stopwatch.Elapsed,
                RequestFailureFormatter.FormatTimeout());
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
                RequestFailureFormatter.FormatException(ex));
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

    private static async Task<string?> ReadTruncatedBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ToTruncatedBody(body);
    }

    private static string? ToTruncatedBody(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        return BodyTruncator.Truncate(body);
    }
}
