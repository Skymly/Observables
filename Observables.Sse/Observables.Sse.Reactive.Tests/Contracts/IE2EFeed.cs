using System;
using Observables.Sse;

namespace Observables.Sse.Reactive.Tests.Contracts;

public sealed record Tick(int Value);

[Sse]
public interface IE2EFeed
{
    [SseEvent("price")]
    IObservable<string> Prices { get; }

    [SseEvent("tick")]
    IObservable<Tick> Ticks { get; }

    [Business.SseEvent("price")]
    [SseEvent]
    IObservable<string> Heartbeats { get; }
}

public interface IPriceFeed
{
    [SseEvent("price")]
    IObservable<string> Data { get; }
}

public interface IHeartbeatFeed
{
    [SseEvent]
    IObservable<string> Data { get; }
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
