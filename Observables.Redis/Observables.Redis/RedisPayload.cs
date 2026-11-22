namespace Observables.Redis;

/// <summary>Payload conversion helpers shared by Redis Pub/Sub bridges.</summary>
internal static class RedisPayload
{
    internal static T Deserialize<T>(byte[] payload) => RedisPayloadSerializers.Deserialize<T>(payload);
}
