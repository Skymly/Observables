using System.IO;
using System.Net.Http;
using System.Text;
using R3;

namespace Observables.Sse;

/// <summary>Bridges an SSE <c>text/event-stream</c> endpoint to R3 <see cref="Observable{T}"/>.</summary>
public static class SseObservable
{
    /// <summary>
    /// Opens a connection per subscription and emits payloads of the named event,
    /// deserialized to <typeparamref name="T"/> (string passthrough; JSON on net8.0+).
    /// </summary>
    public static Observable<T> FromEvent<T>(SseConnection connection, string eventName) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, connection.Endpoint);
                request.Headers.Accept.ParseAdd("text/event-stream");

                using var response = await connection.HttpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

#if NET8_0_OR_GREATER
                using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
#else
                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                using var reader = new StreamReader(stream, Encoding.UTF8);

                while (!ct.IsCancellationRequested)
                {
                    var sseEvent = await SseProtocol.ReadEventAsync(reader).ConfigureAwait(false);
                    if (sseEvent is null)
                    {
                        observer.OnCompleted();
                        return;
                    }

                    if (sseEvent.Value.EventName == eventName)
                    {
                        observer.OnNext(SseProtocol.Deserialize<T>(sseEvent.Value.Data));
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
        });
}
