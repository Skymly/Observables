using System.ComponentModel;
using MQTTnet.Client;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Mqtt;

/// <summary>Creates source-generated MQTT topic proxy implementations.</summary>
public static class MqttService
{
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
