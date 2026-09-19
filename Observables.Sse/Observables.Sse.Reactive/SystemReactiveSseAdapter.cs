using System.Reactive.Linq;
using System.Net.Http;
using Observables.Sse;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Sse.Reactive;

/// <summary>Bridges an SSE <c>text/event-stream</c> endpoint to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveSseAdapter
{
    /// <summary>
    /// Opens a connection per subscription and emits payloads of the named event,
    /// deserialized to <typeparamref name="T"/> (string passthrough; JSON on net8.0+).
    /// Header-phase <see cref="HttpClient.Timeout"/> and other non-subscription faults
    /// terminate through <c>OnError</c>. Payload deserialization errors are also terminal.
    /// Local subscription cancellation stays silent.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static IObservable<T> FromEvent<T>(SseConnection connection, string eventName) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                await SseProtocol
                    .SubscribeAsync<T>(connection, eventName, observer.OnNext, observer.OnCompleted, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == ct)
            {
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
}
