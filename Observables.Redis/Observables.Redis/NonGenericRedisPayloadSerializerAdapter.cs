using System.Diagnostics.CodeAnalysis;

namespace Observables.Redis;

#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
internal sealed class NonGenericRedisPayloadSerializerAdapter<
#if NET8_0_OR_GREATER
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
    )]
#endif
    T>(IRedisPayloadSerializer serializer) : IRedisPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    public byte[] Serialize(T value) => serializer.Serialize(typeof(T), value);
}
