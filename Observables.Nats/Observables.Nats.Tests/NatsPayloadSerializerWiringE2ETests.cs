using System.Text;
using NATS.Client.Core;
using Observables.Nats.Tests.Contracts;
using Observables.Nats.Tests.Infrastructure;
using R3;

namespace Observables.Nats.Tests;

/// <summary>
/// Asserts that a serializer registered with <see cref="NatsPayloadSerializers"/> is the one
/// generated proxies actually use. A JSON-vs-JSON round trip would pass either way, so the
/// registered serializer here writes a deliberately non-JSON wire format.
/// </summary>
[Collection(nameof(NatsTestServerCollection))]
public sealed class NatsPayloadSerializerWiringE2ETests(NatsTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    static NatsOpts CreateOpts(string url) => new() { Url = url };

    [Fact]
    public async Task Registered_serializer_is_called_for_publish_and_subscribe()
    {
        var serializer = new PipeDelimitedReadingSerializer();
        NatsPayloadSerializers.Register(serializer);
        try
        {
            await using var subscriber = new NatsConnection(CreateOpts(fixture.Server.Url));
            await using var publisher = new NatsConnection(CreateOpts(fixture.Server.Url));
            var subHub = NatsService.For<IE2EPocoHub>(subscriber);
            var pubHub = NatsService.For<IE2EPocoHub>(publisher);

            using var cts = new CancellationTokenSource(DefaultTimeout);
            var receive = subHub.Readings.FirstAsync(cts.Token);
            await NatsE2EHelpers.PublishUntilReceivedAsync(
                async ct => await pubHub
                    .PublishReading(new Reading { DeviceId = "probe", Celsius = 21 })
                    .FirstAsync(ct),
                receive,
                cts.Token);

            var reading = await receive;
            Assert.Equal("probe", reading.DeviceId);
            Assert.Equal(21, reading.Celsius);
            Assert.True(serializer.SerializeCount > 0, "the registered serializer never serialized a payload");
            Assert.True(serializer.DeserializeCount > 0, "the registered serializer never deserialized a payload");
        }
        finally
        {
            NatsPayloadSerializers.Unregister<Reading>();
        }
    }

    [Fact]
    public async Task Registered_serializer_decides_the_wire_format()
    {
        NatsPayloadSerializers.Register(new PipeDelimitedReadingSerializer());
        try
        {
            await using var listener = new NatsConnection(CreateOpts(fixture.Server.Url));
            await using var publisher = new NatsConnection(CreateOpts(fixture.Server.Url));
            var pubHub = NatsService.For<IE2EPocoHub>(publisher);

            using var cts = new CancellationTokenSource(DefaultTimeout);
            await using var raw = await listener.SubscribeCoreAsync<byte[]>("e2e.poco", cancellationToken: cts.Token);
            await listener.PingAsync(cts.Token);

            var receive = raw.Msgs.ReadAsync(cts.Token).AsTask();
            await NatsE2EHelpers.PublishUntilReceivedAsync(
                async ct => await pubHub
                    .PublishReading(new Reading { DeviceId = "probe", Celsius = 21 })
                    .FirstAsync(ct),
                receive,
                cts.Token);

            var wire = Encoding.UTF8.GetString((await receive).Data!);
            Assert.Equal("probe|21", wire);
        }
        finally
        {
            NatsPayloadSerializers.Unregister<Reading>();
        }
    }

    sealed class PipeDelimitedReadingSerializer : INatsPayloadSerializer<Reading>
    {
        int _serializeCount;
        int _deserializeCount;

        public int SerializeCount => Volatile.Read(ref _serializeCount);

        public int DeserializeCount => Volatile.Read(ref _deserializeCount);

        public Reading Deserialize(ReadOnlySpan<byte> payload)
        {
            Interlocked.Increment(ref _deserializeCount);
            var parts = Encoding.UTF8.GetString(payload).Split('|');
            return new Reading { DeviceId = parts[0], Celsius = int.Parse(parts[1]) };
        }

        public byte[] Serialize(Reading value)
        {
            Interlocked.Increment(ref _serializeCount);
            return Encoding.UTF8.GetBytes($"{value.DeviceId}|{value.Celsius}");
        }
    }
}
