using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Npgsql;

namespace Observables.Postgres;

internal static class PostgresProtocol
{
    static readonly Regex ChannelNameRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static void ValidateChannelName(string channel)
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

    internal static string QuoteIdent(string channel) =>
        "\"" + channel.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    internal static async Task ListenAsync(
        NpgsqlConnection connection,
        string channel,
        Action<string> onPayload,
        Action onCompleted,
        Action<Exception> onError,
        bool completeOnCancel,
        CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        ValidateChannelName(channel);

        void Handler(object sender, NpgsqlNotificationEventArgs args)
        {
            if (string.Equals(args.Channel, channel, StringComparison.Ordinal))
            {
                onPayload(args.Payload ?? string.Empty);
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
                await listen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await connection.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            connection.Notification -= Handler;
            try
            {
                await UnlistenAsync(connection, channel).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error ??= ex;
            }
        }

        if (error is not null)
        {
            onError(error);
        }
        else if (completeOnCancel || !cancellationToken.IsCancellationRequested)
        {
            onCompleted();
        }
    }

    internal static async Task NotifyAsync(
        NpgsqlConnection connection,
        string channel,
        string? payload,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        ValidateChannelName(channel);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
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
    }

    [RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
    internal static Task NotifySerializedAsync<T>(
        NpgsqlConnection connection,
        string channel,
        T payload,
        CancellationToken userToken,
        CancellationToken pumpToken) =>
        NotifyAsync(connection, channel, PostgresPayload.SerializeToText(payload), userToken, pumpToken);

    internal static async Task UnlistenAsync(NpgsqlConnection connection, string channel)
    {
        NpgsqlOperationInProgressException? lastInProgress = null;
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
            catch (NpgsqlOperationInProgressException ex)
            {
                lastInProgress = ex;
                await Task.Delay(25, CancellationToken.None).ConfigureAwait(false);
            }
        }

        if (lastInProgress is not null)
        {
            throw lastInProgress;
        }

        throw new TimeoutException("UNLISTEN did not complete while WaitAsync was in progress.");
    }
}
