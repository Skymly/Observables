using NATS.Client.Core;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Nats;

internal static class NatsProtocol
{
    internal static async Task PublishEmptyAsync(
        INatsConnection connection,
        string subject,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await connection.PublishAsync(subject, string.Empty, cancellationToken: linked.Token).ConfigureAwait(false);
    }

    internal static async Task PublishBytesAsync(
        INatsConnection connection,
        string subject,
        ReadOnlyMemory<byte> payload,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        if (payload.IsEmpty)
        {
            await connection.PublishAsync(subject, string.Empty, cancellationToken: linked.Token).ConfigureAwait(false);
        }
        else
        {
            await connection.PublishAsync(subject, payload, cancellationToken: linked.Token).ConfigureAwait(false);
        }
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    internal static async Task PublishAsync<T>(
        INatsConnection connection,
        string subject,
        T payload,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await connection.PublishAsync(subject, payload, cancellationToken: linked.Token).ConfigureAwait(false);
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    internal static async Task SubscribeAsync<T>(
        INatsConnection connection,
        string subject,
        Action<T> onNext,
        Action onCompleted,
        CancellationToken cancellationToken)
    {
        await foreach (var msg in connection.SubscribeAsync<T>(subject, cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            onNext(msg.Data!);
        }

        onCompleted();
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("NATS payload serialization may use reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("NATS payload serialization may use reflection.")]
#endif
    internal static async Task<TResponse> RequestAsync<TRequest, TResponse>(
        INatsConnection connection,
        string subject,
        TRequest request,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        var reply = await connection
            .RequestAsync<TRequest, TResponse>(subject, request, cancellationToken: linked.Token)
            .ConfigureAwait(false);
        return reply.Data ?? throw new InvalidOperationException("NATS request returned null payload.");
    }
}
