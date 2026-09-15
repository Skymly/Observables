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
    static readonly ConcurrentDictionary<string, INatsConnection> NamedConnections =
        new(StringComparer.Ordinal);

    static readonly ConcurrentDictionary<Type, string> ProxyNames = new();

    /// <summary>
    /// Registers a connection under the name an interface declares with <c>[Nats(connectionName)]</c>, making it
    /// resolvable through <see cref="For{T}()"/>. Registering the same name twice replaces the connection.
    /// </summary>
    public static void RegisterConnection(string name, INatsConnection connection)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        NamedConnections[name] = connection;
    }

    /// <summary>Registers the name a generated proxy was declared with.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterProxyName(Type natsInterfaceType, string connectionName)
    {
        if (natsInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(natsInterfaceType));
        }

        if (connectionName is null)
        {
            throw new ArgumentNullException(nameof(connectionName));
        }

        ProxyNames[natsInterfaceType] = connectionName;
    }

    /// <summary>Registers a source-generated subject proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type natsInterfaceType,
        Func<INatsConnection, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<INatsConnection>.Register(natsInterfaceType, factory);
#else
    public static void RegisterGeneratedFactory(Type natsInterfaceType, Func<INatsConnection, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<INatsConnection>.Register(natsInterfaceType, factory);
#endif

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(INatsConnection connection) => (T)For(typeof(T), connection);
#else
    public static T For<T>(INatsConnection connection) => (T)For(typeof(T), connection);
#endif

    /// <summary>
    /// Creates a proxy over the connection registered under the name <typeparamref name="T"/> declares with
    /// <c>[Nats(connectionName)]</c>.
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
    /// Creates a proxy over the connection registered under the name <paramref name="natsInterfaceType"/>
    /// declares with <c>[Nats(connectionName)]</c>.
    /// </summary>
#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type natsInterfaceType
    )
#else
    public static object For(Type natsInterfaceType)
#endif
    {
        if (natsInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(natsInterfaceType));
        }

        if (!ProxyNames.TryGetValue(natsInterfaceType, out var connectionName))
        {
            throw new InvalidOperationException(
                natsInterfaceType.Name
                + " is not declared with a name. Either write [Nats(\"<name>\")] on the interface and register a "
                + "connection with NatsService.RegisterConnection, or resolve it with "
                + "NatsService.For<T>(connection).");
        }

        if (!NamedConnections.TryGetValue(connectionName, out var connection))
        {
            throw new InvalidOperationException(
                "No NATS connection is registered under \""
                + connectionName
                + "\". Call NatsService.RegisterConnection(\""
                + connectionName
                + "\", connection) before resolving "
                + natsInterfaceType.Name
                + ".");
        }

        return For(natsInterfaceType, connection);
    }

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

        return global::Observables.GeneratedProxyFactoryRegistry<INatsConnection>.Create(
            natsInterfaceType,
            connection,
            natsInterfaceType.Name
            + " does not have a generated NATS proxy. Ensure the interface is marked with [Nats], "
            + "Observables.Nats source generators are referenced, and the project was rebuilt.");
    }
}
