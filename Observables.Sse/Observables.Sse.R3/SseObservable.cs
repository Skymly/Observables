using System.Net.Http;
using R3;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Sse;

/// <summary>Bridges an SSE <c>text/event-stream</c> endpoint to R3 <see cref="Observable{T}"/>.</summary>
public static class SseObservable
{
    /// <summary>
    /// Opens a connection per subscription and emits payloads of the named event,
    /// deserialized to <typeparamref name="T"/> (string passthrough; JSON on net8.0+).
    /// Header-phase <see cref="HttpClient.Timeout"/> and other non-subscription faults
    /// complete the observable with failure. Payload deserialization errors are also terminal.
    /// Local subscription cancellation stays silent.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromEvent<T>(SseConnection connection, string eventName) =>
        R3AsyncCreate.Create<T>(async (observer, ct) =>
        {
            await SseProtocol
                .SubscribeAsync<T>(connection, eventName, observer.OnNext, observer.OnCompleted, ct)
                .ConfigureAwait(false);
        });
}
