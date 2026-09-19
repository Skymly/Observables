using NATS.Client.Core;
using R3;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Nats;

/// <summary>Bridges NATS client APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class NatsObservable
{
    public static Observable<Unit> FromPublish(
        INatsConnection connection,
        string subject,
        CancellationToken cancellationToken = default) =>
        FromPublish(connection, subject, ReadOnlyMemory<byte>.Empty, cancellationToken);

    public static Observable<Unit> FromPublish(
        INatsConnection connection,
        string subject,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await NatsProtocol.PublishBytesAsync(connection, subject, payload, cancellationToken, ct).ConfigureAwait(false);
            return Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    public static Observable<Unit> FromPublish<T>(
        INatsConnection connection,
        string subject,
        T payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await NatsProtocol.PublishAsync(connection, subject, payload, cancellationToken, ct).ConfigureAwait(false);
            return Unit.Default;
        });

    /// <summary>
    /// Subscribes to <paramref name="subject"/>. Reply and deserialize errors are terminal on both
    /// backends (R3 completes with failure; Rx <c>OnError</c>). Unlike MQTT, later messages are not delivered after an error.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    public static Observable<T> FromSubscribe<T>(INatsConnection connection, string subject) =>
        R3AsyncCreate.Create<T>(async (observer, ct) =>
        {
            await NatsProtocol
                .SubscribeAsync<T>(connection, subject, observer.OnNext, observer.OnCompleted, ct)
                .ConfigureAwait(false);
        });

    /// <summary>
    /// Sends a request and emits the reply. Malformed or error replies fail the observable instead of
    /// returning <c>default(TResponse)</c>.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    public static Observable<TResponse> FromRequest<TRequest, TResponse>(
        INatsConnection connection,
        string subject,
        TRequest request,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
            await NatsProtocol.RequestAsync<TRequest, TResponse>(connection, subject, request, cancellationToken, ct)
                .ConfigureAwait(false));
}
