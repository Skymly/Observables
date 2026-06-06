using System.Text;

namespace Observables.Mqtt.Tests;

public sealed class MqttPayloadSerializerTests
{
    [Fact]
    public void Primitive_deserializes_string_and_byte_array()
    {
        var serializer = PrimitiveMqttPayloadSerializer.Instance;

        Assert.Equal("ping", serializer.Deserialize(typeof(string), "ping"u8.ToArray()));
        Assert.Equal(new byte[] { 1, 2 }, serializer.Deserialize(typeof(byte[]), new byte[] { 1, 2 }));
    }

    [Fact]
    public void Primitive_serializes_string_and_byte_array()
    {
        var serializer = PrimitiveMqttPayloadSerializer.Instance;

        Assert.Equal("ping"u8.ToArray(), serializer.Serialize(typeof(string), "ping"));
        Assert.Equal(new byte[] { 3 }, serializer.Serialize(typeof(byte[]), new byte[] { 3 }));
    }

    [Fact]
    public void Primitive_throws_for_unsupported_types()
    {
        var serializer = PrimitiveMqttPayloadSerializer.Instance;

        Assert.Throws<NotSupportedException>(() => serializer.Deserialize(typeof(int), "1"u8.ToArray()));
        Assert.Throws<NotSupportedException>(() => serializer.Serialize(typeof(int), 1));
    }

    [Fact]
    public void Interface_overloads_deserialize_and_serialize_generically()
    {
        IMqttPayloadSerializer serializer = PrimitiveMqttPayloadSerializer.Instance;

        Assert.Equal("ping", serializer.Deserialize<string>("ping"u8.ToArray()));
        Assert.Equal("ping", serializer.Deserialize<string>((ReadOnlySpan<byte>)"ping"u8.ToArray()));
        Assert.Equal("ping"u8.ToArray(), serializer.Serialize("ping"));
        Assert.Equal(new byte[] { 4 }, serializer.Deserialize<byte[]>(new byte[] { 4 }));
    }

    [Fact]
    public void Serializers_expose_non_generic_overloads()
    {
        Assert.Equal("ping", MqttPayloadSerializers.Deserialize(typeof(string), "ping"u8.ToArray()));
        Assert.Equal("ping"u8.ToArray(), MqttPayloadSerializers.Serialize(typeof(string), "ping"));
        Assert.Equal("ping", MqttPayloadSerializers.Deserialize<string>("ping"u8.ToArray()));
        Assert.Equal("ping"u8.ToArray(), MqttPayloadSerializers.Serialize<string>("ping"));
    }

    [Fact]
    public void Default_serializer_roundtrips_json_payloads()
    {
        var serializer = DefaultMqttPayloadSerializer.Instance;
        var payload = serializer.Serialize(typeof(TemperatureReading), new TemperatureReading { DeviceId = "a", Celsius = 21.5 });
        var reading = (TemperatureReading)serializer.Deserialize(typeof(TemperatureReading), payload);

        Assert.Equal("a", reading.DeviceId);
        Assert.Equal(21.5, reading.Celsius);
    }

    [Fact]
    public void Register_non_generic_serializer_for_type()
    {
        var previous = MqttPayloadSerializers.Current;
        try
        {
            MqttPayloadSerializers.Current = PrimitiveMqttPayloadSerializer.Instance;
            MqttPayloadSerializers.Register<int>(new Int32Serializer());

            Assert.Equal(7, MqttPayloadSerializers.Deserialize<int>("7"u8.ToArray()));
            Assert.Equal("7"u8.ToArray(), MqttPayloadSerializers.Serialize(7));
        }
        finally
        {
            MqttPayloadSerializers.Unregister<int>();
            MqttPayloadSerializers.Current = previous;
        }
    }

    [Fact]
    public void Current_can_be_replaced_with_custom_serializer()
    {
        var previous = MqttPayloadSerializers.Current;
        try
        {
            MqttPayloadSerializers.Current = new PrefixSerializer();
            Assert.Equal("hello", MqttPayloadSerializers.Deserialize<string>("prefix:hello"u8.ToArray()));
        }
        finally
        {
            MqttPayloadSerializers.Current = previous;
        }
    }

    [Fact]
    public void Register_generic_serializer_takes_precedence_over_current()
    {
        var previous = MqttPayloadSerializers.Current;
        try
        {
            MqttPayloadSerializers.Current = PrimitiveMqttPayloadSerializer.Instance;
            MqttPayloadSerializers.Register<int>(
                static payload => int.Parse(System.Text.Encoding.UTF8.GetString(payload)),
                static value => System.Text.Encoding.UTF8.GetBytes(value.ToString()));

            Assert.Equal(42, MqttPayloadSerializers.Deserialize<int>("42"u8.ToArray()));
            Assert.Equal("99"u8.ToArray(), MqttPayloadSerializers.Serialize(99));
        }
        finally
        {
            MqttPayloadSerializers.Unregister<int>();
            MqttPayloadSerializers.Current = previous;
        }
    }

    [Fact]
    public void Unregister_removes_typed_serializer()
    {
        var previous = MqttPayloadSerializers.Current;
        try
        {
            MqttPayloadSerializers.Current = PrimitiveMqttPayloadSerializer.Instance;
            MqttPayloadSerializers.Register<string>(
                static _ => "typed",
                static _ => "typed"u8.ToArray());

            Assert.Equal("typed", MqttPayloadSerializers.Deserialize<string>(Array.Empty<byte>()));
            Assert.True(MqttPayloadSerializers.Unregister<string>());
            Assert.Equal(string.Empty, MqttPayloadSerializers.Deserialize<string>(Array.Empty<byte>()));
        }
        finally
        {
            MqttPayloadSerializers.Unregister<string>();
            MqttPayloadSerializers.Current = previous;
        }
    }

    sealed class PrefixSerializer : IMqttPayloadSerializer
    {
        public object Deserialize(Type payloadType, ReadOnlySpan<byte> payload)
        {
            if (payloadType != typeof(string))
            {
                throw new NotSupportedException();
            }

            var text = Encoding.UTF8.GetString(payload);
            return text.StartsWith("prefix:", StringComparison.Ordinal) ? text["prefix:".Length..] : text;
        }

        public byte[] Serialize(Type payloadType, object? value) =>
            Encoding.UTF8.GetBytes("prefix:" + (value as string ?? string.Empty));
    }

    sealed class Int32Serializer : IMqttPayloadSerializer
    {
        public object Deserialize(Type payloadType, ReadOnlySpan<byte> payload)
        {
            if (payloadType != typeof(int))
            {
                throw new NotSupportedException();
            }

            return int.Parse(Encoding.UTF8.GetString(payload));
        }

        public byte[] Serialize(Type payloadType, object? value)
        {
            if (payloadType != typeof(int))
            {
                throw new NotSupportedException();
            }

            return Encoding.UTF8.GetBytes(((int)value!).ToString());
        }
    }

    sealed class TemperatureReading
    {
        public string DeviceId { get; init; } = string.Empty;

        public double Celsius { get; init; }
    }
}
