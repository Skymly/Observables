using Observables.Sse;
using R3;

namespace Observables.Sse.Tests.Contracts;

public sealed record Tick(int Value);

[Sse]
public interface IE2EFeed
{
    [SseEvent("price")]
    Observable<string> Prices { get; }

    [SseEvent("tick")]
    Observable<Tick> Ticks { get; }

    [Business.SseEvent("price")]
    [SseEvent]
    Observable<string> Heartbeats { get; }
}

public interface IPriceFeed
{
    [SseEvent("price")]
    Observable<string> Data { get; }
}

public interface IHeartbeatFeed
{
    [SseEvent]
    Observable<string> Data { get; }
}

[Sse]
public interface ICombinedFeed : IPriceFeed, IHeartbeatFeed;

public static class Business
{
    public sealed class SseEventAttribute(string eventName) : Attribute
    {
        public string EventName { get; } = eventName;
    }
}
