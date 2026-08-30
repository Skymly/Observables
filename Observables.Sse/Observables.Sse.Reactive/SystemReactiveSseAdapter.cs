using System.Reactive.Linq;
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
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
}
