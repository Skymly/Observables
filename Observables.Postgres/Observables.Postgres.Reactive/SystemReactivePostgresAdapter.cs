using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Npgsql;

namespace Observables.Postgres.Reactive;

/// <summary>Bridges Npgsql LISTEN/NOTIFY to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactivePostgresAdapter
{
    static readonly Regex ChannelNameRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    static readonly TimeSpan ListenWaitSlice = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Hot stream: <c>LISTEN</c> on <paramref name="channel"/> and emit notification payloads until disposed.
    /// Runs a WaitAsync loop on <paramref name="connection"/> for the
    /// subscription lifetime. Use a dedicated non-pooled connection; do not share it with concurrent commands.
    /// </summary>
    public static IObservable<string> FromListen(NpgsqlConnection connection, string channel)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        ValidateChannelName(channel);

        return Observable.Create<string>(async (observer, ct) =>
        {
            void Handler(object sender, NpgsqlNotificationEventArgs args)
            {
                if (string.Equals(args.Channel, channel, StringComparison.Ordinal))
                {
                    observer.OnNext(args.Payload ?? string.Empty);
                }
            }

            connection.Notification += Handler;
            Exception? error = null;
            try
            {
                await using (var listen = new NpgsqlCommand(
                    "LISTEN " + QuoteIdent(channel) + ";",
                    connection))
                {
                    await listen.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                await WaitForNotificationsAsync(connection, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                connection.Notification -= Handler;
                await UnlistenBestEffortAsync(connection, channel).ConfigureAwait(false);
            }

            if (error is not null)
            {
                observer.OnError(error);
            }
            else if (!ct.IsCancellationRequested)
            {
                observer.OnCompleted();
            }
        });
    }

    /// <summary>
    /// Hot stream: <c>LISTEN</c> on <paramref name="channel"/> and deserialize notification payloads to
    /// <typeparamref name="T"/> until disposed.
    /// </summary>
    [RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
    public static IObservable<T> FromListen<T>(NpgsqlConnection connection, string channel)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        ValidateChannelName(channel);

        return Observable.Create<T>(async (observer, ct) =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var hasError = false;
            Exception? error = null;

            void Handler(object sender, NpgsqlNotificationEventArgs args)
            {
                if (!string.Equals(args.Channel, channel, StringComparison.Ordinal))
                {
                    return;
                }

                try
                {
                    observer.OnNext(PostgresPayload.Deserialize<T>(args.Payload));
                }
                catch (Exception ex)
                {
                    hasError = true;
                    observer.OnError(ex);
                    try
                    {
                        cts.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }

            connection.Notification += Handler;
            try
            {
                await using (var listen = new NpgsqlCommand(
                    "LISTEN " + QuoteIdent(channel) + ";",
                    connection))
                {
                    await listen.ExecuteNonQueryAsync(cts.Token).ConfigureAwait(false);
                }

                await WaitForNotificationsAsync(connection, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                connection.Notification -= Handler;
                await UnlistenBestEffortAsync(connection, channel).ConfigureAwait(false);
            }

            if (hasError)
            {
                return;
            }

            if (error is not null)
            {
                observer.OnError(error);
            }
            else if (!ct.IsCancellationRequested)
            {
                observer.OnCompleted();
            }
        });
    }

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
        CancellationToken cancellationToken = default)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        ValidateChannelName(channel);

        return Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await using var command = new NpgsqlCommand(
                "SELECT pg_notify(@channel, @payload);",
                connection)
            {
                Parameters =
                {
                    new("channel", channel),
                    new("payload", payload ?? string.Empty),
                },
            };
            await command.ExecuteNonQueryAsync(linked.Token).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });
    }

    /// <summary>Cold stream: serialize <paramref name="payload"/> and send <c>NOTIFY</c> when subscribed.</summary>
    [RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
    public static IObservable<System.Reactive.Unit> FromNotify<T>(
        NpgsqlConnection connection,
        string channel,
        T payload,
        CancellationToken cancellationToken = default)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        ValidateChannelName(channel);

        return Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            var text = PostgresPayload.SerializeToText(payload);
            await using var command = new NpgsqlCommand(
                "SELECT pg_notify(@channel, @payload);",
                connection)
            {
                Parameters =
                {
                    new("channel", channel),
                    new("payload", text),
                },
            };
            await command.ExecuteNonQueryAsync(linked.Token).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });
    }

    static async Task WaitForNotificationsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await connection.WaitAsync(ListenWaitSlice, cancellationToken).ConfigureAwait(false);
        }
    }

    static async Task UnlistenBestEffortAsync(NpgsqlConnection connection, string channel)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                if ((connection.State & ConnectionState.Open) == 0)
                {
                    return;
                }

                await using var unlisten = new NpgsqlCommand(
                    "UNLISTEN " + QuoteIdent(channel) + ";",
                    connection);
                await unlisten.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (NpgsqlOperationInProgressException)
            {
                await Task.Delay(25, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                return;
            }
        }
    }

    static void ValidateChannelName(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("PostgreSQL channel name must be non-empty.", nameof(channel));
        }

        if (channel.Length > 63 || !ChannelNameRegex.IsMatch(channel))
        {
            throw new ArgumentException(
                "PostgreSQL channel name must be at most 63 characters and match [A-Za-z_][A-Za-z0-9_]*.",
                nameof(channel));
        }
    }

    static string QuoteIdent(string channel) =>
        "\"" + channel.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
