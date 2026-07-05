namespace Stressor.Core.Tests;

using System.Net;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> responseFactory;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        this.responseFactory = responseFactory;
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException();
        }

        return responseFactory(request);
    }
}
