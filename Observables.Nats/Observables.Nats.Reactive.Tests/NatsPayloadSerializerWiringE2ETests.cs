using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using NATS.Client.Core;
using Observables.Nats;
using Observables.Nats.Reactive.Tests.Contracts;
using Observables.Nats.Tests.Infrastructure;

namespace Observables.Nats.Reactive.Tests;

/// <summary>
/// The System.Reactive counterpart of the R3 wiring test: both adapters funnel through
/// <c>NatsProtocol</c>, so this covers the second path into the same bridge.
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
            var subHub = NatsService.For<IE2EPocoHubReactive>(subscriber);
            var pubHub = NatsService.For<IE2EPocoHubReactive>(publisher);

            using var cts = new CancellationTokenSource(DefaultTimeout);
            var receive = subHub.Readings.Timeout(DefaultTimeout).FirstAsync().ToTask(cts.Token);
            await NatsE2EHelpers.PublishUntilReceivedAsync(
                async ct => await pubHub
                    .PublishReading(new Reading { DeviceId = "probe", Celsius = 21 })
                    .Timeout(DefaultTimeout)
                    .FirstAsync()
                    .ToTask(ct),
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
