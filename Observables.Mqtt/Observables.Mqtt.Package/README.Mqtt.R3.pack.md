# Observables.Mqtt.R3

Declarative MQTT topic proxies powered by Roslyn and MQTTnet. The generated proxies return [R3](https://github.com/Cysharp/R3) `Observable<T>` streams.

## Install

```bash
dotnet add package Observables.Mqtt.R3
dotnet add package Observables.Mqtt.MQTTnet4
```

## Quick start

This console example connects to an MQTT broker listening on localhost port 1883. It subscribes to the temperature topic and sends an empty ping command.

```csharp
using System;
using System.Threading;
using MQTTnet;
using MQTTnet.Client;
using Observables.Mqtt;
using Observables.Mqtt.MQTTnet4;
using R3;

using var client = new MqttFactory().CreateMqttClient();
var options = new MqttClientOptionsBuilder()
    .WithTcpServer("localhost", 1883)
    .Build();
await client.ConnectAsync(options, CancellationToken.None);

var topics = MqttService.For<ISensorTopics>(MqttNetTransport.Wrap(client));
using var readings = topics.Temperature.Subscribe(value => Console.WriteLine(value));
using var ping = topics.Ping("room-1").Subscribe(_ => Console.WriteLine("Ping sent"));
Console.ReadLine();

[Mqtt]
public interface ISensorTopics
{
    [MqttSubscribe("/sensors/temperature")]
    Observable<string> Temperature { get; }

    [MqttPublish("sensors/{id}/ping")]
    Observable<Unit> Ping(string id);
}
```

Generated publish methods send empty payloads. For raw bytes, subscribe to `MqttObservable.FromPublish(MqttNetTransport.Wrap(client), topic, payload, cancellationToken)`.

Subscription attributes accept exact topic filters and standard `+` and `#` wildcards. Shared subscription filters starting with `$share/` produce `OBS5006`.

## Diagnostics

`OBS5001`–`OBS5006` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
