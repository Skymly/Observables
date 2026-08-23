using System.ComponentModel;
using System.Net.WebSockets;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.WebSocket;

/// <summary>Creates source-generated WebSocket proxy implementations.</summary>
public static class WebSocketService
{
    /// <summary>Registers a source-generated WebSocket proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type wsInterfaceType,
        Func<ClientWebSocket, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<ClientWebSocket>.Register(wsInterfaceType, factory);
#else
    public static void RegisterGeneratedFactory(Type wsInterfaceType, Func<ClientWebSocket, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<ClientWebSocket>.Register(wsInterfaceType, factory);
#endif

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

        return global::Observables.GeneratedProxyFactoryRegistry<ClientWebSocket>.Create(
            wsInterfaceType,
            socket,
            wsInterfaceType.Name
            + " does not have a generated WebSocket proxy. Ensure the interface is marked with [WebSocket], "
            + "Observables.WebSocket source generators are referenced, and the project was rebuilt.");
    }
}
