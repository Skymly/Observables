using System.Reactive.Linq;
using System.Threading;
using NATS.Client.Core;
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
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await connection.PublishAsync(subject, string.Empty, cancellationToken: linked.Token)
                .ConfigureAwait(false);
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
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await connection.PublishAsync(subject, payload, cancellationToken: linked.Token).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    public static IObservable<T> FromSubscribe<T>(INatsConnection connection, string subject) =>
        Observable.Create<T>(observer =>
        {
            var cts = new CancellationTokenSource();
            _ = SubscribeAsync();

            return () => cts.Cancel();

            async Task SubscribeAsync()
            {
                try
                {
                    await foreach (var msg in connection.SubscribeAsync<T>(subject, cancellationToken: cts.Token)
                                       .ConfigureAwait(false))
                    {
                        observer.OnNext(msg.Data!);
                    }

                    observer.OnCompleted();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    observer.OnError(ex);
                }
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
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            var reply = await connection
                .RequestAsync<TRequest, TResponse>(subject, request, cancellationToken: linked.Token)
                .ConfigureAwait(false);
            return reply.Data ?? throw new InvalidOperationException("NATS request returned null payload.");
        });
}
