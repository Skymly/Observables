using System.Reactive.Linq;
using NATS.Client.Core;
using Observables.Nats;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Nats.Reactive;

/// <summary>Bridges NATS client APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveNatsAdapter
{
    public static IObservable<System.Reactive.Unit> FromPublish(
        INatsConnection connection,
        string subject,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await NatsProtocol.PublishEmptyAsync(connection, subject, cancellationToken, ct).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    public static IObservable<System.Reactive.Unit> FromPublish<T>(
        INatsConnection connection,
        string subject,
        T payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await NatsProtocol.PublishAsync(connection, subject, payload, cancellationToken, ct).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    public static IObservable<T> FromSubscribe<T>(INatsConnection connection, string subject) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                await NatsProtocol
                    .SubscribeAsync<T>(connection, subject, observer.OnNext, observer.OnCompleted, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observer.OnError(ex);
            }
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    public static IObservable<TResponse> FromRequest<TRequest, TResponse>(
        INatsConnection connection,
        string subject,
        TRequest request,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
            await NatsProtocol.RequestAsync<TRequest, TResponse>(connection, subject, request, cancellationToken, ct)
                .ConfigureAwait(false));
}
