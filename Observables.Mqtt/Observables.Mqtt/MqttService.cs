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
    static readonly ConcurrentDictionary<Type, Func<IMqttClient, object>> GeneratedFactories = new();

    /// <summary>Registers a source-generated topic proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type mqttInterfaceType,
        Func<IMqttClient, object> factory)
#else
    public static void RegisterGeneratedFactory(Type mqttInterfaceType, Func<IMqttClient, object> factory)
#endif
    {
        if (mqttInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(mqttInterfaceType));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        GeneratedFactories[mqttInterfaceType] = factory;
    }

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(IMqttClient client) => (T)For(typeof(T), client);
#else
    public static T For<T>(IMqttClient client) => (T)For(typeof(T), client);
#endif

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

        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (GeneratedFactories.TryGetValue(mqttInterfaceType, out var factory))
        {
            return factory(client);
        }

        throw new InvalidOperationException(
            mqttInterfaceType.Name
            + " does not have a generated MQTT proxy. Ensure the interface is marked with [Mqtt], "
            + "Observables.Mqtt source generators are referenced, and the project was rebuilt.");
    }
}
