using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Observables.Postgres;

/// <summary>UTF-8 JSON serializer for complex PostgreSQL NOTIFY payloads.</summary>
[RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
public sealed class JsonPostgresPayloadSerializer : IPostgresPayloadSerializer
{
    public static JsonPostgresPayloadSerializer Instance { get; } = new();

    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    JsonPostgresPayloadSerializer()
    {
    }

    public object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload)
    {
        var json = Encoding.UTF8.GetString(payload);
        var value = JsonSerializer.Deserialize(json, payloadType, DefaultOptions);
        if (value is null)
        {
            throw new InvalidOperationException("PostgreSQL payload deserialized to null.");
        }

        return value;
    }

    public byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, payloadType, DefaultOptions));
    }
}
