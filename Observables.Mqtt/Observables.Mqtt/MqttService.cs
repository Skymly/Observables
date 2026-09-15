using System.Collections.Concurrent;
using System.ComponentModel;
using MQTTnet.Client;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Mqtt;

/// <summary>Creates source-generated MQTT topic proxy implementations.</summary>
public static class MqttService
{
    static readonly ConcurrentDictionary<string, IMqttClient> NamedClients = new(StringComparer.Ordinal);

    static readonly ConcurrentDictionary<Type, string> ProxyNames = new();

    /// <summary>
    /// Registers a client under the name an interface declares with <c>[Mqtt(clientName)]</c>, making it
    /// resolvable through <see cref="For{T}()"/>. Registering the same name twice replaces the client.
    /// </summary>
    public static void RegisterClient(string name, IMqttClient client)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        NamedClients[name] = client;
    }

    /// <summary>Registers the name a generated proxy was declared with.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterProxyName(Type mqttInterfaceType, string clientName)
    {
        if (mqttInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(mqttInterfaceType));
        }

        if (clientName is null)
        {
            throw new ArgumentNullException(nameof(clientName));
        }

        ProxyNames[mqttInterfaceType] = clientName;
    }

    /// <summary>Registers a source-generated topic proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type mqttInterfaceType,
        Func<IMqttClient, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<IMqttClient>.Register(mqttInterfaceType, factory);
#else
    public static void RegisterGeneratedFactory(Type mqttInterfaceType, Func<IMqttClient, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<IMqttClient>.Register(mqttInterfaceType, factory);
#endif

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(IMqttClient client) => (T)For(typeof(T), client);
#else
    public static T For<T>(IMqttClient client) => (T)For(typeof(T), client);
#endif

    /// <summary>
    /// Creates a proxy over the client registered under the name <typeparamref name="T"/> declares with
    /// <c>[Mqtt(clientName)]</c>.
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
    /// Creates a proxy over the client registered under the name <paramref name="mqttInterfaceType"/>
    /// declares with <c>[Mqtt(clientName)]</c>.
    /// </summary>
#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type mqttInterfaceType
    )
#else
    public static object For(Type mqttInterfaceType)
#endif
    {
        if (mqttInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(mqttInterfaceType));
        }

        if (!ProxyNames.TryGetValue(mqttInterfaceType, out var clientName))
        {
            throw new InvalidOperationException(
                mqttInterfaceType.Name
                + " is not declared with a name. Either write [Mqtt(\"<name>\")] on the interface and register a "
                + "client with MqttService.RegisterClient, or resolve it with MqttService.For<T>(client).");
        }

        if (!NamedClients.TryGetValue(clientName, out var client))
        {
            throw new InvalidOperationException(
                "No MQTT client is registered under \""
                + clientName
                + "\". Call MqttService.RegisterClient(\""
                + clientName
                + "\", client) before resolving "
                + mqttInterfaceType.Name
                + ".");
        }

        return For(mqttInterfaceType, client);
    }

#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type mqttInterfaceType,
        IMqttClient client
    )
#else
    public static object For(Type mqttInterfaceType, IMqttClient client)
#endif
    {
        if (mqttInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(mqttInterfaceType));
        }

        return global::Observables.GeneratedProxyFactoryRegistry<IMqttClient>.Create(
            mqttInterfaceType,
            client,
            mqttInterfaceType.Name
            + " does not have a generated MQTT proxy. Ensure the interface is marked with [Mqtt], "
            + "Observables.Mqtt source generators are referenced, and the project was rebuilt.");
    }
}
