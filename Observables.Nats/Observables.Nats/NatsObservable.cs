using NATS.Client.Core;
using R3;

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
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            if (payload.IsEmpty)
            {
                await connection.PublishAsync(subject, string.Empty, cancellationToken: linked.Token)
                    .ConfigureAwait(false);
            }
            else
            {
                await connection.PublishAsync(subject, payload, cancellationToken: linked.Token).ConfigureAwait(false);
            }

            return Unit.Default;
        });

    public static Observable<Unit> FromPublish<T>(
        INatsConnection connection,
        string subject,
        T payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await connection.PublishAsync(subject, payload, cancellationToken: linked.Token).ConfigureAwait(false);
            return Unit.Default;
        });

    public static Observable<T> FromSubscribe<T>(INatsConnection connection, string subject) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                await foreach (var msg in connection.SubscribeAsync<T>(subject, cancellationToken: ct).ConfigureAwait(false))
                {
                    observer.OnNext(msg.Data!);
                }

                observer.OnCompleted();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observer.OnErrorResume(ex);
            }
        });

    public static Observable<TResponse> FromRequest<TRequest, TResponse>(
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
