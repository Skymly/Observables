using System.Diagnostics.CodeAnalysis;

namespace Observables.Mqtt;

/// <summary>Global MQTT payload serializer used by <see cref="MqttObservable"/> and generated proxies.</summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(MqttTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(MqttTrimAnnotations.JsonPayload)]
#endif
public static class MqttPayloadSerializers
{
    static IMqttPayloadSerializer s_current = DefaultMqttPayloadSerializer.Instance;
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, object> s_typed = new();

    /// <summary>Fallback serializer when no typed registration exists for the requested payload type.</summary>
    public static IMqttPayloadSerializer Current
    {
        get => s_current;
        set => s_current = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Registers a typed serializer. Takes precedence over <see cref="Current"/> for that <typeparamref name="T"/>.</summary>
    public static void Register<T>(IMqttPayloadSerializer<T> serializer) =>
        s_typed[typeof(T)] = serializer ?? throw new ArgumentNullException(nameof(serializer));

    /// <summary>Registers <typeparamref name="T"/> using a non-generic <see cref="IMqttPayloadSerializer"/>.</summary>
    public static void Register<
#if NET8_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )]
#endif
        T>(IMqttPayloadSerializer serializer) =>
        Register(new NonGenericMqttPayloadSerializerAdapter<T>(
            serializer ?? throw new ArgumentNullException(nameof(serializer))));

    /// <summary>Registers a typed serializer from delegates (<paramref name="deserialize"/> receives a copied payload buffer).</summary>
    public static void Register<T>(Func<byte[], T> deserialize, Func<T, byte[]> serialize) =>
        Register(new DelegateMqttPayloadSerializer<T>(deserialize, serialize));

    /// <summary>Removes a typed registration, if present.</summary>
    public static bool Unregister<T>() => s_typed.TryRemove(typeof(T), out _);

    /// <summary>Deserializes a payload using <see cref="Current"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload) =>
        Current.Deserialize(payloadType, payload);

    /// <summary>Deserializes a payload buffer using <see cref="Current"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(MqttTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(MqttTrimAnnotations.JsonPayload)]
#endif
    public static object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        byte[] payload) =>
        Current.Deserialize(payloadType, payload);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static T Deserialize<T>(ReadOnlySpan<byte> payload)
    {
        if (TryGetTypedSerializer<T>(out var typed))
        {
            return typed.Deserialize(payload);
        }

        return Current.Deserialize<T>(payload);
    }

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(MqttTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(MqttTrimAnnotations.JsonPayload)]
#endif
    public static T Deserialize<T>(byte[] payload) =>
        Deserialize<T>((ReadOnlySpan<byte>)payload);

    /// <summary>Serializes <paramref name="value"/> using <see cref="Current"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value) =>
        Current.Serialize(payloadType, value);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static byte[] Serialize<T>(T value)
    {
        if (TryGetTypedSerializer<T>(out var typed))
        {
            return typed.Serialize(value!);
        }

        return Current.Serialize<T>(value);
    }

    static bool TryGetTypedSerializer<T>(out IMqttPayloadSerializer<T> serializer)
    {
        if (s_typed.TryGetValue(typeof(T), out var instance) && instance is IMqttPayloadSerializer<T> typed)
        {
            serializer = typed;
            return true;
        }

        serializer = null!;
        return false;
    }
}
