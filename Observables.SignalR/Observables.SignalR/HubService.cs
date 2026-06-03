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
    static readonly ConcurrentDictionary<Type, Func<HubConnection, object>> GeneratedFactories = new();

    /// <summary>Registers a source-generated hub proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterGeneratedFactory(Type hubInterfaceType, Func<HubConnection, object> factory)
    {
        if (hubInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(hubInterfaceType));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        GeneratedFactories[hubInterfaceType] = factory;
    }

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(HubConnection connection) => (T)For(typeof(T), connection);
#else
    public static T For<T>(HubConnection connection) => (T)For(typeof(T), connection);
#endif

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

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (GeneratedFactories.TryGetValue(hubInterfaceType, out var factory))
        {
            return factory(connection);
        }

        throw new InvalidOperationException(
            hubInterfaceType.Name
            + " does not have a generated hub proxy. Ensure the interface is marked with [Hub], "
            + "Observables.SignalR source generators are referenced, and the project was rebuilt.");
    }
}
