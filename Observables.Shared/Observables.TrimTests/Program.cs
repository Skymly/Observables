// ILLink trim-analysis smoke project.
//
// This program is never executed. Its sole purpose is to compile, trigger
// the R3 source generators for all 7 domains, and let ILLink verify that
// RequiresUnreferencedCode / RequiresDynamicCode / DynamicallyAccessedMembers
// annotations are correct under <PublishTrimmed>true</PublishTrimmed> +
// <TrimMode>full</TrimMode>.
//
// Each domain defines a minimal interface with the domain's attribute(s),
// then calls XxxService.For<T>() with a mock/null connection. The source
// generator produces a proxy implementation, and ILLink analyses the entire
// call graph — runtime reflection fallback, generated factory registration,
// and the generated proxy itself.

using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using Grpc.Core;
using Microsoft.AspNetCore.SignalR.Client;
using MQTTnet.Client;
using NATS.Client.Core;
using Observables.Grpc;
using Observables.Mqtt;
using Observables.Nats;
using Observables.RestAPI;
using Observables.SignalR;
using Observables.Sse;
using Observables.WebSocket;
using R3;

// ── Program entry point ──────────────────────────────────────────────────

internal static class TrimProgram
{
    /// <summary>
    /// Entry point for the trim-analysis smoke program.
    /// </summary>
    [UnconditionalSuppressMessage("TrimAnalysis", "IL2026", Justification = "The entry point intentionally invokes the annotated trim smoke method.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The entry point intentionally invokes the annotated trim smoke method.")]
    public static void Main() => RunTrimSmoke();

    /// <summary>
    /// Exercises every domain's <c>XxxService.For&lt;T&gt;()</c> factory.
    /// Annotated with <see cref="RequiresUnreferencedCodeAttribute"/> and
    /// <see cref="RequiresDynamicCodeAttribute"/> because RestAPI's
    /// <see cref="RestService"/> declares these at class level; the trim
    /// analyser propagates them to this call site. The remaining domains
    /// use <see cref="DynamicallyAccessedMembersAttribute"/> on the type
    /// parameter, which the source-generated proxies satisfy without
    /// requiring unreferenced code.
    /// </summary>
    [RequiresUnreferencedCode("Calls RestService which uses reflection on interface methods and DTO types.")]
    [RequiresDynamicCode("Calls RestService which dynamically creates generated client types.")]
    private static void RunTrimSmoke()
    {
        // RestAPI — most Requires* annotations (JSON serialisation + reflection).
        var userApi = RestService.For<Observables.TrimTests.RestAPI.ITrimUserApi>(
            new HttpClient { BaseAddress = new Uri("https://trim.example.com") });
        _ = userApi;

        // Mqtt — JSON payload serializer Requires* annotations.
        var mqttHub = MqttService.For<Observables.TrimTests.Mqtt.ITrimMqttHub>(null!);
        _ = mqttHub;

        // Nats — JSON payload serializer Requires* annotations.
        var natsHub = NatsService.For<Observables.TrimTests.Nats.ITrimNatsHub>(null!);
        _ = natsHub;

        // SignalR — hub proxy reflection annotations.
        var hub = HubService.For<Observables.TrimTests.SignalR.ITrimHub>(
            new HubConnectionBuilder().Build());
        _ = hub;

        // WebSocket — built-in ClientWebSocket.
        var ws = WebSocketService.For<Observables.TrimTests.WebSocket.ITrimWebSocket>(
            new ClientWebSocket());
        _ = ws;

        // Grpc — CallInvoker is abstract; null-forgiving suffices for IL analysis.
        var grpc = GrpcService.For<Observables.TrimTests.Grpc.ITrimGrpc>(null!);
        _ = grpc;

        // Sse — SseConnection wraps an HttpClient + endpoint URI.
        var sse = SseService.For<Observables.TrimTests.Sse.ITrimSseFeed>(
            new SseConnection(
                new HttpClient(),
                new Uri("https://trim.example.com/events")));
        _ = sse;
    }
}

// ── Domain interfaces ────────────────────────────────────────────────────

namespace Observables.TrimTests.RestAPI
{
    public interface ITrimUserApi
    {
        [Get("/api/users/{id}")]
        Task<TrimUser> GetUser(int id);

        [Get("/api/users")]
        Observable<TrimUser> GetUsers();
    }

    public sealed class TrimUser
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}

namespace Observables.TrimTests.Mqtt
{
    [Mqtt]
    public interface ITrimMqttHub
    {
        [MqttSubscribe("trim/ping")]
        Observable<string> Ping { get; }

        [MqttPublish("trim/ping")]
        Observable<Unit> PublishPing();
    }
}

namespace Observables.TrimTests.Nats
{
    [Nats]
    public interface ITrimNatsHub
    {
        [NatsSubscribe("trim.ping")]
        Observable<string> Ping { get; }

        [NatsPublish("trim.ping")]
        Observable<Unit> PublishPing();

        [NatsRequest("trim.echo")]
        Observable<string> Echo(string message);
    }
}

namespace Observables.TrimTests.SignalR
{
    [Hub]
    public interface ITrimHub
    {
        [HubInvoke]
        Observable<int> Add(int a, int b);

        [HubSend]
        Observable<Unit> EchoSend(string text);

        [HubOn("Notify")]
        Observable<string> Notify { get; }
    }
}

namespace Observables.TrimTests.WebSocket
{
    [WebSocket]
    public interface ITrimWebSocket
    {
        [WebSocketConnect]
        Observable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);

        [WebSocketClose]
        Observable<Unit> Close(CancellationToken cancellationToken = default);

        [WebSocketSend("ping")]
        Observable<Unit> Ping(CancellationToken cancellationToken = default);

        [WebSocketReceive("echo")]
        Observable<string> EchoText { get; }
    }
}

namespace Observables.TrimTests.Grpc
{
    [Grpc("trim.TrimService")]
    public interface ITrimGrpc
    {
        [GrpcUnary("UnaryEcho")]
        Observable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);

        [GrpcServerStream("ServerStreamEcho")]
        Observable<string> ServerStreamEcho(string request, CancellationToken cancellationToken = default);
    }
}

namespace Observables.TrimTests.Sse
{
    public sealed record TrimTick(int Value);

    [Sse]
    public interface ITrimSseFeed
    {
        [SseEvent("price")]
        Observable<string> Prices { get; }

        [SseEvent("tick")]
        Observable<TrimTick> Ticks { get; }
    }
}
