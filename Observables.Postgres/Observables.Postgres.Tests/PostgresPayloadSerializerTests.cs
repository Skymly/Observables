using System.Text;

namespace Observables.Postgres.Tests;

public sealed class PostgresPayloadSerializerTests
{
    [Fact]
    public void Primitive_roundtrips_string()
    {
        var serializer = PrimitivePostgresPayloadSerializer.Instance;

        Assert.Equal("ping", serializer.Deserialize(typeof(string), "ping"u8.ToArray()));
        Assert.Equal("ping"u8.ToArray(), serializer.Serialize(typeof(string), "ping"));
    }

    [Fact]
    public void Primitive_throws_for_unsupported_types()
    {
        var serializer = PrimitivePostgresPayloadSerializer.Instance;

        Assert.Throws<NotSupportedException>(() => serializer.Deserialize(typeof(int), "1"u8.ToArray()));
        Assert.Throws<NotSupportedException>(() => serializer.Serialize(typeof(int), 1));
    }

    [Fact]
    public void Default_serializer_roundtrips_json_payloads()
    {
        var serializer = DefaultPostgresPayloadSerializer.Instance;
        var payload = serializer.Serialize(typeof(TemperatureReading), new TemperatureReading { DeviceId = "a", Celsius = 21.5 });
        var reading = (TemperatureReading)serializer.Deserialize(typeof(TemperatureReading), payload);

        Assert.Equal("a", reading.DeviceId);
        Assert.Equal(21.5, reading.Celsius);
    }

    [Fact]
    public void Register_delegate_serializer_for_type()
    {
        var previous = PostgresPayloadSerializers.Current;
        try
        {
            PostgresPayloadSerializers.Current = PrimitivePostgresPayloadSerializer.Instance;
            PostgresPayloadSerializers.Register<int>(
                static bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
                static value => Encoding.UTF8.GetBytes(value.ToString()));

            Assert.Equal(42, PostgresPayloadSerializers.Deserialize<int>("42"u8.ToArray()));
            Assert.Equal("99"u8.ToArray(), PostgresPayloadSerializers.Serialize(99));
        }
        finally
        {
            PostgresPayloadSerializers.Unregister<int>();
            PostgresPayloadSerializers.Current = previous;
        }
    }

    sealed class TemperatureReading
    {
        public string DeviceId { get; init; } = string.Empty;

        public double Celsius { get; init; }
    }
}
