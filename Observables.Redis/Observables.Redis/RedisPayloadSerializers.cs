using System.Diagnostics.CodeAnalysis;

namespace Observables.Redis;

/// <summary>Global Redis payload serializer used by <see cref="RedisObservable"/> and generated proxies.</summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
public static class RedisPayloadSerializers
{
    static IRedisPayloadSerializer s_current = DefaultRedisPayloadSerializer.Instance;
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, object> s_typed = new();

    /// <summary>Fallback serializer when no typed registration exists for the requested payload type.</summary>
    public static IRedisPayloadSerializer Current
    {
        get => s_current;
        set => s_current = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Registers a typed serializer. Takes precedence over <see cref="Current"/> for that <typeparamref name="T"/>.</summary>
    public static void Register<T>(IRedisPayloadSerializer<T> serializer) =>
        s_typed[typeof(T)] = serializer ?? throw new ArgumentNullException(nameof(serializer));

    /// <summary>Registers <typeparamref name="T"/> using a non-generic <see cref="IRedisPayloadSerializer"/>.</summary>
    public static void Register<
#if NET8_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )]
#endif
        T>(IRedisPayloadSerializer serializer) =>
        Register(new NonGenericRedisPayloadSerializerAdapter<T>(
            serializer ?? throw new ArgumentNullException(nameof(serializer))));

    /// <summary>Registers a typed serializer from delegates (<paramref name="deserialize"/> receives a copied payload buffer).</summary>
    public static void Register<T>(Func<byte[], T> deserialize, Func<T, byte[]> serialize) =>
        Register(new DelegateRedisPayloadSerializer<T>(deserialize, serialize));

    /// <summary>Removes a typed registration, if present.</summary>
    public static bool Unregister<T>() => s_typed.TryRemove(typeof(T), out _);

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

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
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

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static T Deserialize<T>(byte[] payload) =>
        Deserialize<T>((ReadOnlySpan<byte>)payload);

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

    static bool TryGetTypedSerializer<T>(out IRedisPayloadSerializer<T> serializer)
    {
        if (s_typed.TryGetValue(typeof(T), out var instance) && instance is IRedisPayloadSerializer<T> typed)
        {
            serializer = typed;
            return true;
        }

        serializer = null!;
        return false;
    }
}
