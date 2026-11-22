#if NET8_0_OR_GREATER
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Observables;

internal static class SourceGeneratedJson
{
    internal static JsonTypeInfo<T> Resolve<T>(JsonSerializerContext context) =>
        context.GetTypeInfo(typeof(T)) as JsonTypeInfo<T> ??
        throw new NotSupportedException($"No source-generated JSON metadata for '{typeof(T)}' is registered in '{context.GetType()}'.");

    internal static T DeserializePayload<T>(ReadOnlySpan<byte> payload, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Deserialize(payload, typeInfo) is { } value
            ? value
            : throw new InvalidOperationException("JSON payload deserialized to null.");

    internal static byte[] SerializePayload<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    internal static T DeserializeRawPayload<T>(ReadOnlySpan<byte> payload, string registrationMethod)
    {
        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)payload.ToArray();
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)Encoding.UTF8.GetString(payload);
        }

        throw MissingMetadata<T>(registrationMethod);
    }

    internal static byte[] SerializeRawPayload<T>(T value, string registrationMethod)
    {
        if (typeof(T) == typeof(byte[]))
        {
            return value is byte[] bytes ? bytes : Array.Empty<byte>();
        }

        if (typeof(T) == typeof(string))
        {
            return Encoding.UTF8.GetBytes(value as string ?? string.Empty);
        }

        throw MissingMetadata<T>(registrationMethod);
    }

    static NotSupportedException MissingMetadata<T>(string registrationMethod) =>
        new($"JSON reflection is disabled for '{typeof(T)}'. Register source-generated metadata with {registrationMethod}.");
}
#endif
