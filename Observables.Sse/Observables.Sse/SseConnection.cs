using System.Net.Http;

namespace Observables.Sse;

/// <summary>Binds an <see cref="HttpClient"/> to an SSE endpoint for generated proxies.</summary>
public sealed class SseConnection
{
    public SseConnection(HttpClient httpClient, Uri endpoint)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    /// <summary>The HTTP client used to open the <c>text/event-stream</c> connection.</summary>
    public HttpClient HttpClient { get; }

    /// <summary>The SSE endpoint URI.</summary>
    public Uri Endpoint { get; }
}
