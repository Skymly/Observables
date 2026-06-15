using System.Diagnostics.CodeAnalysis;

namespace Observables.Nats;

#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(NatsTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(NatsTrimAnnotations.JsonPayload)]
#endif
internal sealed class NonGenericNatsPayloadSerializerAdapter<
#if NET8_0_OR_GREATER
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
    )]
#endif
    T>(INatsPayloadSerializer serializer) : INatsPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    public byte[] Serialize(T value) => serializer.Serialize(typeof(T), value);
}
