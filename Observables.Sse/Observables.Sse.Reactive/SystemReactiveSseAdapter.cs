using System.IO;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Observables.Sse.Reactive;

/// <summary>Bridges an SSE <c>text/event-stream</c> endpoint to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveSseAdapter
{
    /// <summary>
    /// Opens a connection per subscription and emits payloads of the named event,
    /// deserialized to <typeparamref name="T"/> (string passthrough; JSON on net8.0+).
    /// </summary>
    public static IObservable<T> FromEvent<T>(SseConnection connection, string eventName) =>
        System.Reactive.Linq.Observable.Create<T>(observer =>
        {
            var cts = new CancellationTokenSource();

            _ = RunAsync();

            return Disposable.Create(() => cts.Cancel());

            async Task RunAsync()
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, connection.Endpoint);
                    request.Headers.Accept.ParseAdd("text/event-stream");

                    using var response = await connection.HttpClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                        .ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

#if NET8_0_OR_GREATER
                    using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
#else
                    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    while (!cts.IsCancellationRequested)
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
                    observer.OnError(ex);
                }
            }
        });
}
