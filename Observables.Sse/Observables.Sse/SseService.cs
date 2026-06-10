using System.Collections.Concurrent;
using System.ComponentModel;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Sse;

/// <summary>Creates source-generated SSE proxy implementations.</summary>
public static class SseService
{
    static readonly ConcurrentDictionary<Type, Func<SseConnection, object>> GeneratedFactories = new();

    /// <summary>Registers a source-generated SSE proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterGeneratedFactory(Type sseInterfaceType, Func<SseConnection, object> factory)
    {
        if (sseInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(sseInterfaceType));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        GeneratedFactories[sseInterfaceType] = factory;
    }

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

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (GeneratedFactories.TryGetValue(sseInterfaceType, out var factory))
        {
            return factory(connection);
        }

        throw new InvalidOperationException(
            sseInterfaceType.Name
            + " does not have a generated SSE proxy. Ensure the interface is marked with [Sse], "
            + "Observables.Sse source generators are referenced, and the project was rebuilt.");
    }
}
