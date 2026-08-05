namespace Observables.Redis.Tests.Infrastructure;

internal static class RedisE2EHelpers
{
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
}
