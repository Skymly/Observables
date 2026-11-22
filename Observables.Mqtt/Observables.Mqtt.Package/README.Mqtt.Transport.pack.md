# Observables MQTT transport

Observables generates MQTT topic proxies for R3 and System.Reactive. Install one reactive backend package and one MQTTnet adapter.

| Role | NuGet package |
|------|---------------|
| R3 proxy generator and bridge | `Observables.Mqtt.R3` |
| System.Reactive proxy generator and bridge | `Observables.Mqtt.Reactive` |
| MQTTnet 4 adapter | `Observables.Mqtt.MQTTnet4` |
| MQTTnet 5 adapter (.NET 8 or later) | `Observables.Mqtt.MQTTnet5` |
| Shared MQTT runtime and transport interface | `Observables.Mqtt` (installed transitively) |

Import `Observables.Mqtt.MQTTnet4` or `Observables.Mqtt.MQTTnet5`, then pass `MqttNetTransport.Wrap(client)` to `MqttService.For<T>()` or `MqttService.RegisterClient()`. Your application owns the client and its connection.

See the [Observables repository](https://github.com/Skymly/Observables) for MQTT proxy examples.
