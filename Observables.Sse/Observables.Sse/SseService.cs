using System.ComponentModel;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Sse;

/// <summary>Creates source-generated SSE proxy implementations.</summary>
public static class SseService
{
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
