using Observables.Redis;
using System.Text;

namespace Observables.Redis.Tests;

public sealed class RedisPayloadSerializerTests
{
    [Fact]
    public void Non_generic_entry_points_use_current_not_typed_registration()
    {
        var previous = RedisPayloadSerializers.Current;
        try
        {
            RedisPayloadSerializers.Current = PrimitiveRedisPayloadSerializer.Instance;
            RedisPayloadSerializers.Register<string>(
                static _ => "typed",
                static _ => "typed"u8.ToArray());

            Assert.Equal("typed", RedisPayloadSerializers.Deserialize<string>(Array.Empty<byte>()));
            Assert.Equal(string.Empty, RedisPayloadSerializers.Deserialize(typeof(string), Array.Empty<byte>()));
            Assert.Equal("typed"u8.ToArray(), RedisPayloadSerializers.Serialize<string>("x"));
            Assert.Equal("x"u8.ToArray(), RedisPayloadSerializers.Serialize(typeof(string), "x"));
        }
        finally
        {
            RedisPayloadSerializers.Unregister<string>();
            RedisPayloadSerializers.Current = previous;
        }
    }
}
