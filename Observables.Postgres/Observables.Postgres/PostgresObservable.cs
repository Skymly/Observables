using System.Data;
using System.Text.RegularExpressions;
using Npgsql;
using R3;

namespace Observables.Postgres;

/// <summary>Bridges Npgsql LISTEN/NOTIFY to R3 <see cref="Observable{T}"/>.</summary>
public static class PostgresObservable
{
    static readonly Regex ChannelNameRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Hot stream: <c>LISTEN</c> on <paramref name="channel"/> and emit notification payloads until disposed.
    /// Runs a WaitAsync loop on <paramref name="connection"/> for the
    /// subscription lifetime. Use a dedicated non-pooled connection; do not share it with concurrent commands.
    /// </summary>
    public static Observable<string> FromListen(NpgsqlConnection connection, string channel)
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
            try
            {
                await using (var listen = new NpgsqlCommand(
                    "LISTEN " + QuoteIdent(channel) + ";",
                    connection))
                {
                    await listen.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                while (!ct.IsCancellationRequested)
                {
                    await connection.WaitAsync(ct).ConfigureAwait(false);
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException)
            {
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
            finally
            {
                connection.Notification -= Handler;
                try
                {
                    if (connection.State == ConnectionState.Open)
                    {
                        await using var unlisten = new NpgsqlCommand(
                            "UNLISTEN " + QuoteIdent(channel) + ";",
                            connection);
                        await unlisten.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Best-effort cleanup when the connection is already broken.
                }
            }
        });
    }

    /// <summary>Cold stream: send <c>NOTIFY</c> with an empty payload when subscribed.</summary>
    public static Observable<Unit> FromNotify(
        NpgsqlConnection connection,
        string channel,
        CancellationToken cancellationToken = default) =>
        FromNotify(connection, channel, payload: null, cancellationToken);

    /// <summary>Cold stream: send <c>NOTIFY</c> with an optional string payload when subscribed.</summary>
    public static Observable<Unit> FromNotify(
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
            return Unit.Default;
        });
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
