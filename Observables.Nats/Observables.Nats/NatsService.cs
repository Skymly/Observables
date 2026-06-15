using System.Collections.Concurrent;
using System.ComponentModel;
using NATS.Client.Core;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Nats;

/// <summary>Creates source-generated NATS subject proxy implementations.</summary>
public static class NatsService
{
    static readonly ConcurrentDictionary<Type, Func<INatsConnection, object>> GeneratedFactories = new();

    /// <summary>Registers a source-generated subject proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type natsInterfaceType,
        Func<INatsConnection, object> factory)
#else
    public static void RegisterGeneratedFactory(Type natsInterfaceType, Func<INatsConnection, object> factory)
#endif
    {
        if (natsInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(natsInterfaceType));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        GeneratedFactories[natsInterfaceType] = factory;
    }

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(INatsConnection connection) => (T)For(typeof(T), connection);
#else
    public static T For<T>(INatsConnection connection) => (T)For(typeof(T), connection);
#endif

#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type natsInterfaceType,
        INatsConnection connection
    )
#else
    public static object For(Type natsInterfaceType, INatsConnection connection)
#endif
    {
        if (natsInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(natsInterfaceType));
        }

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (GeneratedFactories.TryGetValue(natsInterfaceType, out var factory))
        {
            return factory(connection);
        }

        throw new InvalidOperationException(
            natsInterfaceType.Name
            + " does not have a generated NATS proxy. Ensure the interface is marked with [Nats], "
            + "Observables.Nats source generators are referenced, and the project was rebuilt.");
    }
}
