using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.SignalR;

/// <summary>Creates source-generated hub proxy implementations.</summary>
public static class HubService
{
    static readonly ConcurrentDictionary<string, HubConnection> NamedConnections =
        new(StringComparer.Ordinal);

    static readonly ConcurrentDictionary<Type, string> ProxyNames = new();

    /// <summary>
    /// Registers a connection under the name an interface declares with <c>[Hub(hubName)]</c>, making it
    /// resolvable through <see cref="For{T}()"/>. Registering the same name twice replaces the connection.
    /// </summary>
    public static void RegisterConnection(string name, HubConnection connection)
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
    public static void RegisterProxyName(Type hubInterfaceType, string hubName)
    {
        if (hubInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(hubInterfaceType));
        }

        if (hubName is null)
        {
            throw new ArgumentNullException(nameof(hubName));
        }

        ProxyNames[hubInterfaceType] = hubName;
    }

    /// <summary>Registers a source-generated hub proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type hubInterfaceType,
        Func<HubConnection, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<HubConnection>.Register(hubInterfaceType, factory);
#else
    public static void RegisterGeneratedFactory(Type hubInterfaceType, Func<HubConnection, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<HubConnection>.Register(hubInterfaceType, factory);
#endif

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(HubConnection connection) => (T)For(typeof(T), connection);
#else
    public static T For<T>(HubConnection connection) => (T)For(typeof(T), connection);
#endif

    /// <summary>
    /// Creates a proxy over the connection registered under the name <typeparamref name="T"/> declares with
    /// <c>[Hub(hubName)]</c>.
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
    /// Creates a proxy over the connection registered under the name <paramref name="hubInterfaceType"/>
    /// declares with <c>[Hub(hubName)]</c>.
    /// </summary>
#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type hubInterfaceType
    )
#else
    public static object For(Type hubInterfaceType)
#endif
    {
        if (hubInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(hubInterfaceType));
        }

        if (!ProxyNames.TryGetValue(hubInterfaceType, out var hubName))
        {
            throw new InvalidOperationException(
                hubInterfaceType.Name
                + " is not declared with a name. Either write [Hub(\"<name>\")] on the interface and register a "
                + "connection with HubService.RegisterConnection, or resolve it with HubService.For<T>(connection).");
        }

        if (!NamedConnections.TryGetValue(hubName, out var connection))
        {
            throw new InvalidOperationException(
                "No hub connection is registered under \""
                + hubName
                + "\". Call HubService.RegisterConnection(\""
                + hubName
                + "\", connection) before resolving "
                + hubInterfaceType.Name
                + ".");
        }

        return For(hubInterfaceType, connection);
    }

#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type hubInterfaceType,
        HubConnection connection
    )
#else
    public static object For(Type hubInterfaceType, HubConnection connection)
#endif
    {
        if (hubInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(hubInterfaceType));
        }

        return global::Observables.GeneratedProxyFactoryRegistry<HubConnection>.Create(
            hubInterfaceType,
            connection,
            hubInterfaceType.Name
            + " does not have a generated hub proxy. Ensure the interface is marked with [Hub], "
            + "Observables.SignalR source generators are referenced, and the project was rebuilt.");
    }
}
