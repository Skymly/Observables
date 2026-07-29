using System.Diagnostics.CodeAnalysis;

namespace Observables.Postgres;

[RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
internal sealed class NonGenericPostgresPayloadSerializerAdapter<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
    )]
T>(IPostgresPayloadSerializer serializer) : IPostgresPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    public byte[] Serialize(T value) => serializer.Serialize(typeof(T), value);
}
