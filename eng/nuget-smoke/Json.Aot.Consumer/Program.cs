using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Observables.Mqtt;
using Observables.Redis;
using Observables.RestAPI;
using R3;

if (JsonSerializer.IsReflectionEnabledByDefault)
{
    throw new InvalidOperationException("The JSON smoke consumer requires reflection to be disabled.");
}

var payload = new Reading(17);
var metadata = ReadingContext.Default.Reading;
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
using var http = new HttpClient(new EchoHandler()) { BaseAddress = new Uri("https://json.example/") };
var rest = RestService.For<IReadingApi>(http, new RestApiSettings(
    SystemTextJsonContentSerializer.FromContext(ReadingContext.Default)));
Check(await rest.EchoAsync(payload, timeout.Token) == payload, "REST request and response");

try
{
    await rest.InvalidAsync(timeout.Token);
    throw new InvalidOperationException("Expected a REST validation exception.");
}
catch (ValidationApiException exception)
{
    Check(exception.Content?.Errors["reading"].Single() == "invalid", "REST problem details");
}

MqttPayloadSerializers.RegisterJson(metadata);
var transport = new LoopbackMqttTransport();
var mqtt = MqttService.For<IReadingTopics>(transport);
var next = mqtt.Readings.FirstAsync(timeout.Token);
await transport.Subscribed.WaitAsync(timeout.Token);
await transport.PublishAsync("readings", MqttPayloadSerializers.Serialize(payload), timeout.Token);
Check(await next == payload, "MQTT generated subscription");

RedisPayloadSerializers.RegisterJson(metadata);
Check(RedisPayloadSerializers.Deserialize<Reading>(RedisPayloadSerializers.Serialize(payload)) == payload, "Redis JSON payload");
Check(RedisPayloadSerializers.Deserialize<string>("text"u8) == "text", "Redis raw text payload");
Check(MqttPayloadSerializers.Deserialize<byte[]>("raw"u8).AsSpan().SequenceEqual("raw"u8), "MQTT raw binary payload");

RedisPayloadSerializers.Unregister<Reading>();
try
{
    RedisPayloadSerializers.Serialize(payload);
    throw new InvalidOperationException("Expected missing metadata to be reported.");
}
catch (NotSupportedException exception) when (exception.Message.Contains("RegisterJson", StringComparison.Ordinal))
{
}

Console.WriteLine("Observables source-generated JSON consumer smoke OK");

static void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException($"JSON smoke failed: {description}.");
    }
}

public sealed record Reading(int Value);

[JsonSerializable(typeof(Reading))]
internal sealed partial class ReadingContext : JsonSerializerContext;

[RestApi]
public interface IReadingApi
{
    [Post("/echo")]
    Task<Reading> EchoAsync([Body] Reading input, CancellationToken cancellationToken);

    [Get("/invalid")]
    Task<Reading> InvalidAsync(CancellationToken cancellationToken);
}

[Mqtt]
public interface IReadingTopics
{
    [MqttSubscribe("readings")]
    Observable<Reading> Readings { get; }
}

sealed class EchoHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        if (request.RequestUri?.AbsolutePath == "/invalid")
        {
            response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("""{"status":422,"errors":{"reading":["invalid"]}}""", System.Text.Encoding.UTF8, "application/problem+json"),
            };
        }
        else
        {
            var reading = await request.Content!.ReadFromJsonAsync(ReadingContext.Default.Reading, cancellationToken);
            response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(reading!, ReadingContext.Default.Reading) };
        }

        response.RequestMessage = request;
        return response;
    }
}

sealed class LoopbackMqttTransport : IMqttTransport
{
    readonly TaskCompletionSource subscribed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Subscribed => subscribed.Task;
    public MqttTransportCapabilities Capabilities => MqttTransportCapabilities.None;
    public event Func<string, ReadOnlyMemory<byte>, Task>? MessageReceived;

    public Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken)
    {
        subscribed.TrySetResult();
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PublishAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        MessageReceived?.Invoke(topic, payload) ?? Task.CompletedTask;
}
