using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Text.Json;
using Npgsql;
using Observables.Postgres;
using Observables.Postgres.Reactive.Tests.Contracts;
using Observables.Postgres.Tests.Infrastructure;

namespace Observables.Postgres.Reactive.Tests;

[Collection(nameof(PostgresTestServerCollection))]
public sealed class PostgresClientReactiveTypedPayloadE2ETests(PostgresTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Typed_Listen_deserializes_json_Notify_from_another_session()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var expected = new OrderPayload { OrderId = "ord-42", Quantity = 3 };
        var json = JsonSerializer.Serialize(expected);

        await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
        await listener.OpenAsync(cancellation);
        var hub = PostgresService.For<IE2ETypedHubReactive>(listener);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(DefaultTimeout);
        var receive = hub.Orders.Timeout(DefaultTimeout).FirstAsync().ToTask(timeout.Token);

        await using var notifier = new NpgsqlConnection(fixture.Server.ConnectionString);
        await notifier.OpenAsync(cancellation);
        await using (var notify = new NpgsqlCommand("SELECT pg_notify(@c, @p);", notifier)
        {
            Parameters =
            {
                new("c", "e2e_order"),
                new("p", json),
            },
        })
        {
            for (var attempt = 0; attempt < 20 && !receive.IsCompleted; attempt++)
            {
                await notify.ExecuteNonQueryAsync(cancellation);
                await Task.Delay(50, cancellation);
            }
        }

        var received = await receive;
        Assert.Equal(expected.OrderId, received.OrderId);
        Assert.Equal(expected.Quantity, received.Quantity);
    }

    [Fact]
    public async Task Typed_Notify_roundtrips_via_Listen_observable()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var expected = new OrderPayload { OrderId = "ord-7", Quantity = 9 };

        await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
        await listener.OpenAsync(cancellation);
        var listenHub = PostgresService.For<IE2ETypedHubReactive>(listener);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(DefaultTimeout);
        var receive = listenHub.Orders.Timeout(DefaultTimeout).FirstAsync().ToTask(timeout.Token);

        await using var publisher = new NpgsqlConnection(fixture.Server.ConnectionString);
        await publisher.OpenAsync(cancellation);
        var publishHub = PostgresService.For<IE2ETypedHubReactive>(publisher);

        for (var attempt = 0; attempt < 20 && !receive.IsCompleted; attempt++)
        {
            await publishHub.PublishOrder(expected).Timeout(DefaultTimeout).FirstAsync().ToTask(timeout.Token);
            await Task.Delay(50, cancellation);
        }

        var received = await receive;
        Assert.Equal(expected.OrderId, received.OrderId);
        Assert.Equal(expected.Quantity, received.Quantity);
    }

    [Fact]
    public async Task Custom_serializer_roundtrips_non_json_payload()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var expected = new ColonDelimitedPayload { Kind = "ping", Value = "hello" };
        PostgresPayloadSerializers.Register<ColonDelimitedPayload>(
            static bytes =>
            {
                var text = Encoding.UTF8.GetString(bytes);
                var parts = text.Split(':', 2);
                return new ColonDelimitedPayload
                {
                    Kind = parts[0],
                    Value = parts.Length > 1 ? parts[1] : string.Empty,
                };
            },
            static value => Encoding.UTF8.GetBytes(value.Kind + ":" + value.Value));

        try
        {
            await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
            await listener.OpenAsync(cancellation);
            var listenHub = PostgresService.For<IE2ECustomSerializerHubReactive>(listener);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(DefaultTimeout);
            var receive = listenHub.Messages.Timeout(DefaultTimeout).FirstAsync().ToTask(timeout.Token);

            await using var publisher = new NpgsqlConnection(fixture.Server.ConnectionString);
            await publisher.OpenAsync(cancellation);
            var publishHub = PostgresService.For<IE2ECustomSerializerHubReactive>(publisher);

            for (var attempt = 0; attempt < 20 && !receive.IsCompleted; attempt++)
            {
                await publishHub.Publish(expected).Timeout(DefaultTimeout).FirstAsync().ToTask(timeout.Token);
                await Task.Delay(50, cancellation);
            }

            var received = await receive;
            Assert.Equal(expected.Kind, received.Kind);
            Assert.Equal(expected.Value, received.Value);
        }
        finally
        {
            PostgresPayloadSerializers.Unregister<ColonDelimitedPayload>();
        }
    }
}
