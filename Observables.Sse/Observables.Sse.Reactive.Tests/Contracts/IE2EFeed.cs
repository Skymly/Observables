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

    [SseEvent]
    IObservable<string> Heartbeats { get; }
}
