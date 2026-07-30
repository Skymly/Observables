using System.Diagnostics.CodeAnalysis;

namespace Observables.Postgres;

/// <summary>Payload conversion helpers shared by R3 and Reactive PostgreSQL bridges.</summary>
[RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
internal static class PostgresPayload
{
    internal static T Deserialize<T>(string? payload)
    {
        var text = payload ?? string.Empty;
        return PostgresPayloadSerializers.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(text));
    }

    internal static string SerializeToText<T>(T value)
    {
        var bytes = PostgresPayloadSerializers.Serialize(value);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
