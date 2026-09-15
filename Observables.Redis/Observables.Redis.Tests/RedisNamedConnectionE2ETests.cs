using Observables.Redis;
using Observables.Redis.Tests.Contracts;
using Observables.Redis.Tests.Infrastructure;
using R3;
using StackExchange.Redis;

namespace Observables.Redis.Tests;

[Collection(nameof(RedisTestServerCollection))]
public sealed class RedisNamedConnectionE2ETests(RedisTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Name_on_the_attribute_resolves_the_registered_multiplexer()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var mux = await fixture.Server.ConnectAsync(cancellation);
        RedisService.RegisterConnection("e2e-named", mux);

        var hub = RedisService.For<IE2ENamedHub>();
        var subscriber = mux.GetSubscriber();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = hub.Ping.FirstAsync(cts.Token);
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async _ => await subscriber.PublishAsync(RedisChannel.Literal("e2e.named"), "hello"),
            receive,
            cts.Token);

        Assert.Equal("hello", await receive);
    }

    [Fact]
    public void Resolving_a_name_with_no_registered_multiplexer_says_which_name_is_missing()
    {
        var error = Assert.Throws<InvalidOperationException>(
            static () => RedisService.For(typeof(IE2EUnregisteredHub)));

        Assert.Contains("e2e-unregistered", error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(RedisService.RegisterConnection), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolving_an_unnamed_interface_points_at_the_multiplexer_overload()
    {
        var error = Assert.Throws<InvalidOperationException>(static () => RedisService.For(typeof(IE2EHub)));

        Assert.Contains("[Redis(", error.Message, StringComparison.Ordinal);
        Assert.Contains("For<T>(multiplexer)", error.Message, StringComparison.Ordinal);
    }
}
