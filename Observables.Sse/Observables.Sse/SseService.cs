using System.Collections.Concurrent;
using System.ComponentModel;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Sse;

/// <summary>Creates source-generated SSE proxy implementations.</summary>
public static class SseService
{
    static readonly ConcurrentDictionary<string, SseConnection> NamedEndpoints = new(StringComparer.Ordinal);

    static readonly ConcurrentDictionary<Type, string> ProxyNames = new();

    /// <summary>
    /// Registers a connection under the name an interface declares with <c>[Sse(endpointName)]</c>, making it
    /// resolvable through <see cref="For{T}()"/>. Registering the same name twice replaces the connection.
    /// </summary>
    public static void RegisterEndpoint(string name, SseConnection connection)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        NamedEndpoints[name] = connection;
    }

    /// <summary>Registers the name a generated proxy was declared with.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterProxyName(Type sseInterfaceType, string endpointName)
    {
        if (sseInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(sseInterfaceType));
        }

        if (endpointName is null)
        {
            throw new ArgumentNullException(nameof(endpointName));
        }

        ProxyNames[sseInterfaceType] = endpointName;
    }

    /// <summary>Registers a source-generated SSE proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type sseInterfaceType,
        Func<SseConnection, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<SseConnection>.Register(sseInterfaceType, factory);
#else
    public static void RegisterGeneratedFactory(Type sseInterfaceType, Func<SseConnection, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<SseConnection>.Register(sseInterfaceType, factory);
#endif

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(SseConnection connection) => (T)For(typeof(T), connection);
#else
    public static T For<T>(SseConnection connection) => (T)For(typeof(T), connection);
#endif

    /// <summary>
    /// Creates a proxy over the connection registered under the name <typeparamref name="T"/> declares with
    /// <c>[Sse(endpointName)]</c>.
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
    /// Creates a proxy over the connection registered under the name <paramref name="sseInterfaceType"/>
    /// declares with <c>[Sse(endpointName)]</c>.
    /// </summary>
#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type sseInterfaceType
    )
#else
    public static object For(Type sseInterfaceType)
#endif
    {
        if (sseInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(sseInterfaceType));
        }

        if (!ProxyNames.TryGetValue(sseInterfaceType, out var endpointName))
        {
            throw new InvalidOperationException(
                sseInterfaceType.Name
                + " is not declared with a name. Either write [Sse(\"<name>\")] on the interface and register a "
                + "connection with SseService.RegisterEndpoint, or resolve it with SseService.For<T>(connection).");
        }

        if (!NamedEndpoints.TryGetValue(endpointName, out var connection))
        {
            throw new InvalidOperationException(
                "No SSE connection is registered under \""
                + endpointName
                + "\". Call SseService.RegisterEndpoint(\""
                + endpointName
                + "\", connection) before resolving "
                + sseInterfaceType.Name
                + ".");
        }

        return For(sseInterfaceType, connection);
    }

#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type sseInterfaceType,
        SseConnection connection
    )
#else
    public static object For(Type sseInterfaceType, SseConnection connection)
#endif
    {
        if (sseInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(sseInterfaceType));
        }

        return global::Observables.GeneratedProxyFactoryRegistry<SseConnection>.Create(
            sseInterfaceType,
            connection,
            sseInterfaceType.Name
            + " does not have a generated SSE proxy. Ensure the interface is marked with [Sse], "
            + "Observables.Sse source generators are referenced, and the project was rebuilt.");
    }
}
