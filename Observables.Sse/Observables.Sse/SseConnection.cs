using System.Net.Http;

namespace Observables.Sse;

/// <summary>Binds an <see cref="HttpClient"/> to an SSE endpoint for generated proxies.</summary>
public sealed class SseConnection
{
    /// <summary>Default per-line cap (1 MiB). Exceeding it fails the event.</summary>
    public const int DefaultMaxLineBytes = 1_048_576;

    /// <summary>Default reassembled-event cap (1 MiB). Exceeding it fails the event.</summary>
    public const int DefaultMaxEventBytes = 1_048_576;

    public SseConnection(HttpClient httpClient, Uri endpoint)
        : this(httpClient, endpoint, DefaultMaxLineBytes, DefaultMaxEventBytes)
    {
    }

    /// <summary>Binds an HTTP client, endpoint, and line/event size caps.</summary>
    public SseConnection(HttpClient httpClient, Uri endpoint, int maxLineBytes, int maxEventBytes)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        MaxLineBytes = maxLineBytes;
        MaxEventBytes = maxEventBytes;
    }

    /// <summary>The HTTP client used to open the <c>text/event-stream</c> connection.</summary>
    public HttpClient HttpClient { get; }

    /// <summary>The SSE endpoint URI.</summary>
    public Uri Endpoint { get; }

    /// <summary>Maximum UTF-8 size of one SSE line. A non-positive value fails the subscription.</summary>
    public int MaxLineBytes { get; }

    /// <summary>Maximum UTF-8 size of one reassembled SSE event. A non-positive value fails the subscription.</summary>
    public int MaxEventBytes { get; }
}
