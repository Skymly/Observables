using System.Collections.Concurrent;

namespace Observables;

/// <summary>
/// Shared generated-proxy factory registry used by Feature <c>*Service.For</c> adapters.
/// </summary>
internal static class GeneratedProxyFactoryRegistry<TClient>
    where TClient : class
{
    static readonly ConcurrentDictionary<Type, Func<TClient, object>> Factories = new();

    internal static void Register(Type interfaceType, Func<TClient, object> factory)
    {
        if (interfaceType is null)
        {
            throw new ArgumentNullException(nameof(interfaceType));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        Factories[interfaceType] = factory;
    }

    internal static object Create(Type interfaceType, TClient client, string missingFactoryMessage)
    {
        if (interfaceType is null)
        {
            throw new ArgumentNullException(nameof(interfaceType));
        }

        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (Factories.TryGetValue(interfaceType, out var factory))
        {
            return factory(client);
        }

        throw new InvalidOperationException(missingFactoryMessage);
    }
}
