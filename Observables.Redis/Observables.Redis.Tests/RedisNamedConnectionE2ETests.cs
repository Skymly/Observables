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
        const string name = "e2e-named";
        RedisService.RegisterConnection(name, mux);

        try
        {
            var hub = RedisService.For<IE2ENamedHub>();
            await AssertPingAsync(hub, mux, cancellation);
        }
        finally
        {
            RedisService.TryUnregisterConnection(name, mux);
        }
    }

    [Fact]
    public async Task Unregister_removes_only_the_matching_connection_and_keeps_existing_proxies_usable()
    {
        const string name = "e2e-named";
        var cancellation = TestContext.Current.CancellationToken;
        await using var original = await fixture.Server.ConnectAsync(cancellation);
        await using var replacement = await fixture.Server.ConnectAsync(cancellation);

        RedisService.RegisterConnection(name, original);
        try
        {
            var originalProxy = RedisService.For<IE2ENamedHub>();
            RedisService.RegisterConnection(name, replacement);
            Assert.False(RedisService.TryUnregisterConnection(name, original));

            var replacementProxy = RedisService.For<IE2ENamedHub>();
            Assert.True(RedisService.TryUnregisterConnection(name, replacement));
            Assert.False(RedisService.TryUnregisterConnection(name, replacement));
            Assert.Throws<InvalidOperationException>(RedisService.For<IE2ENamedHub>);
            await AssertPingAsync(originalProxy, original, cancellation);
            await AssertPingAsync(replacementProxy, replacement, cancellation);
        }
        finally
        {
            RedisService.TryUnregisterConnection(name, original);
            RedisService.TryUnregisterConnection(name, replacement);
        }
    }

    static async Task AssertPingAsync(IE2ENamedHub hub, IConnectionMultiplexer mux, CancellationToken cancellation)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var subscriber = mux.GetSubscriber();
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
