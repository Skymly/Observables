using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.WebSockets;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.WebSocket;

/// <summary>Creates source-generated WebSocket proxy implementations.</summary>
public static class WebSocketService
{
    static readonly ConcurrentDictionary<Type, Func<ClientWebSocket, object>> GeneratedFactories = new();

    /// <summary>Registers a source-generated WebSocket proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterGeneratedFactory(Type wsInterfaceType, Func<ClientWebSocket, object> factory)
    {
        if (wsInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(wsInterfaceType));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        GeneratedFactories[wsInterfaceType] = factory;
    }

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(ClientWebSocket socket) => (T)For(typeof(T), socket);
#else
    public static T For<T>(ClientWebSocket socket) => (T)For(typeof(T), socket);
#endif

#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type wsInterfaceType,
        ClientWebSocket socket
    )
#else
    public static object For(Type wsInterfaceType, ClientWebSocket socket)
#endif
    {
        if (wsInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(wsInterfaceType));
        }

        if (socket is null)
        {
            throw new ArgumentNullException(nameof(socket));
        }

        if (GeneratedFactories.TryGetValue(wsInterfaceType, out var factory))
        {
            return factory(socket);
        }

        throw new InvalidOperationException(
            wsInterfaceType.Name
            + " does not have a generated WebSocket proxy. Ensure the interface is marked with [WebSocket], "
            + "Observables.WebSocket source generators are referenced, and the project was rebuilt.");
    }
}
