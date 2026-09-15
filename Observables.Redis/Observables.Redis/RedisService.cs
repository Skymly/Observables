using System.Collections.Concurrent;
using System.ComponentModel;
using StackExchange.Redis;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Redis;

/// <summary>Creates source-generated Redis Pub/Sub proxy implementations.</summary>
public static class RedisService
{
    static readonly ConcurrentDictionary<string, IConnectionMultiplexer> NamedConnections =
        new(StringComparer.Ordinal);

    static readonly ConcurrentDictionary<Type, string> ProxyNames = new();

    /// <summary>
    /// Registers a multiplexer under the name an interface declares with <c>[Redis(connectionName)]</c>, making
    /// it resolvable through <see cref="For{T}()"/>. Registering the same name twice replaces the multiplexer.
    /// </summary>
    public static void RegisterConnection(string name, IConnectionMultiplexer multiplexer)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (multiplexer is null)
        {
            throw new ArgumentNullException(nameof(multiplexer));
        }

        NamedConnections[name] = multiplexer;
    }

    /// <summary>Registers the name a generated proxy was declared with.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterProxyName(Type redisInterfaceType, string connectionName)
    {
        if (redisInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(redisInterfaceType));
        }

        if (connectionName is null)
        {
            throw new ArgumentNullException(nameof(connectionName));
        }

        ProxyNames[redisInterfaceType] = connectionName;
    }

    /// <summary>Registers a source-generated Pub/Sub proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type redisInterfaceType,
        Func<IConnectionMultiplexer, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<IConnectionMultiplexer>.Register(redisInterfaceType, factory);
#else
    public static void RegisterGeneratedFactory(Type redisInterfaceType, Func<IConnectionMultiplexer, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<IConnectionMultiplexer>.Register(redisInterfaceType, factory);
#endif

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(IConnectionMultiplexer multiplexer) => (T)For(typeof(T), multiplexer);
#else
    public static T For<T>(IConnectionMultiplexer multiplexer) => (T)For(typeof(T), multiplexer);
#endif

    /// <summary>
    /// Creates a proxy over the multiplexer registered under the name <typeparamref name="T"/> declares with
    /// <c>[Redis(connectionName)]</c>.
    /// </summary>
#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>() => (T)For(typeof(T));
#else
    public static T For<T>() => (T)For(typeof(T));
#endif

    /// <summary>
    /// Creates a proxy over the multiplexer registered under the name <paramref name="redisInterfaceType"/>
    /// declares with <c>[Redis(connectionName)]</c>.
    /// </summary>
#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type redisInterfaceType
    )
#else
    public static object For(Type redisInterfaceType)
#endif
    {
        if (redisInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(redisInterfaceType));
        }

        if (!ProxyNames.TryGetValue(redisInterfaceType, out var connectionName))
        {
            throw new InvalidOperationException(
                redisInterfaceType.Name
                + " is not declared with a name. Either write [Redis(\"<name>\")] on the interface and register a "
                + "multiplexer with RedisService.RegisterConnection, or resolve it with "
                + "RedisService.For<T>(multiplexer).");
        }

        if (!NamedConnections.TryGetValue(connectionName, out var multiplexer))
        {
            throw new InvalidOperationException(
                "No Redis multiplexer is registered under \""
                + connectionName
                + "\". Call RedisService.RegisterConnection(\""
                + connectionName
                + "\", multiplexer) before resolving "
                + redisInterfaceType.Name
                + ".");
        }

        return For(redisInterfaceType, multiplexer);
    }

#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type redisInterfaceType,
        IConnectionMultiplexer multiplexer
    )
#else
    public static object For(Type redisInterfaceType, IConnectionMultiplexer multiplexer)
#endif
    {
        if (redisInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(redisInterfaceType));
        }

        return global::Observables.GeneratedProxyFactoryRegistry<IConnectionMultiplexer>.Create(
            redisInterfaceType,
            multiplexer,
            redisInterfaceType.Name
            + " does not have a generated Redis proxy. Ensure the interface is marked with [Redis], "
            + "Observables.Redis source generators are referenced, and the project was rebuilt.");
    }
}
