using System.ComponentModel;
using StackExchange.Redis;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Redis;

/// <summary>Creates source-generated Redis Pub/Sub proxy implementations.</summary>
public static class RedisService
{
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
