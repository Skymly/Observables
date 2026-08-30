using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using Npgsql;
using Observables.Postgres;

namespace Observables.Postgres.Reactive;

/// <summary>Bridges Npgsql LISTEN/NOTIFY to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactivePostgresAdapter
{
    /// <summary>
    /// Hot stream: <c>LISTEN</c> on <paramref name="channel"/> and emit notification payloads until disposed.
    /// Runs a WaitAsync loop on <paramref name="connection"/> for the
    /// subscription lifetime. Use a dedicated non-pooled connection; do not share it with concurrent commands.
    /// </summary>
    public static IObservable<string> FromListen(NpgsqlConnection connection, string channel) =>
        Observable.Create<string>(async (observer, ct) =>
        {
            await PostgresProtocol
                .ListenAsync(
                    connection,
                    channel,
                    observer.OnNext,
                    observer.OnCompleted,
                    observer.OnError,
                    completeOnCancel: false,
                    ct)
                .ConfigureAwait(false);
        });

    /// <summary>
    /// Hot stream: <c>LISTEN</c> on <paramref name="channel"/> and deserialize notification payloads to
    /// <typeparamref name="T"/> until disposed.
    /// </summary>
    [RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
    public static IObservable<T> FromListen<T>(NpgsqlConnection connection, string channel) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            await PostgresProtocol
                .ListenAsync(
                    connection,
                    channel,
                    payload =>
                    {
                        try
                        {
                            observer.OnNext(PostgresPayload.Deserialize<T>(payload));
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    },
                    observer.OnCompleted,
                    observer.OnError,
                    completeOnCancel: false,
                    ct)
                .ConfigureAwait(false);
        });

    /// <summary>Cold stream: send <c>NOTIFY</c> with an empty payload when subscribed.</summary>
    public static IObservable<System.Reactive.Unit> FromNotify(
        NpgsqlConnection connection,
        string channel,
        CancellationToken cancellationToken = default) =>
        FromNotify(connection, channel, payload: null, cancellationToken);

    /// <summary>Cold stream: send <c>NOTIFY</c> with an optional string payload when subscribed.</summary>
    public static IObservable<System.Reactive.Unit> FromNotify(
        NpgsqlConnection connection,
        string channel,
        string? payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await PostgresProtocol.NotifyAsync(connection, channel, payload, cancellationToken, ct).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    /// <summary>Cold stream: serialize <paramref name="payload"/> and send <c>NOTIFY</c> when subscribed.</summary>
    [RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
    public static IObservable<System.Reactive.Unit> FromNotify<T>(
        NpgsqlConnection connection,
        string channel,
        T payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await PostgresProtocol
                .NotifySerializedAsync(connection, channel, payload, cancellationToken, ct)
                .ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });
}
