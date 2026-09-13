using NATS.Client.Core;

namespace Observables.Nats.Tests.Infrastructure;

/// <summary>
/// E2E helpers for NATS subscribe races (subscription registration vs publish/request).
/// </summary>
internal static class NatsE2EHelpers
{
    /// <summary>
    /// Publishes repeatedly until <paramref name="receive"/> completes or
    /// <paramref name="cancellationToken"/> fires. Covers the window where
    /// <c>SubscribeAsync</c> has started but the server has not yet registered the interest.
    /// </summary>
    public static async Task PublishUntilReceivedAsync(
        Func<CancellationToken, Task> publish,
        Task receive,
        CancellationToken cancellationToken,
        TimeSpan? retryInterval = null)
    {
        var interval = retryInterval ?? TimeSpan.FromMilliseconds(50);
        while (!receive.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await publish(cancellationToken).ConfigureAwait(false);
            if (receive.IsCompleted)
            {
                return;
            }

            var delay = Task.Delay(interval, cancellationToken);
            if (await Task.WhenAny(receive, delay).ConfigureAwait(false) == receive)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Retries a request-reply until a responder is registered or
    /// <paramref name="cancellationToken"/> fires. NATS returns
    /// <see cref="NatsNoRespondersException"/> immediately when the request
    /// wins the race against <c>SUB</c>, unlike fire-and-forget publish.
    /// </summary>
    public static async Task<T> RequestUntilRespondedAsync<T>(
        Func<CancellationToken, Task<T>> request,
        CancellationToken cancellationToken,
        TimeSpan? retryInterval = null)
    {
        var interval = retryInterval ?? TimeSpan.FromMilliseconds(50);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await request(cancellationToken).ConfigureAwait(false);
            }
            catch (NatsNoRespondersException)
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
