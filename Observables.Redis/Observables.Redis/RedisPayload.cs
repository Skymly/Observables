namespace Observables.Redis;

#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

/// <summary>Payload conversion helpers shared by Redis Pub/Sub bridges.</summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
internal static class RedisPayload
{
    internal static T Deserialize<T>(byte[] payload) => RedisPayloadSerializers.Deserialize<T>(payload);
}
