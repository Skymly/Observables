using System.Diagnostics.CodeAnalysis;

namespace Observables.Mqtt;

#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(MqttTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(MqttTrimAnnotations.JsonPayload)]
#endif
internal sealed class NonGenericMqttPayloadSerializerAdapter<
#if NET8_0_OR_GREATER
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
    )]
#endif
    T>(IMqttPayloadSerializer serializer) : IMqttPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    public byte[] Serialize(T value) => serializer.Serialize(typeof(T), value);
}
