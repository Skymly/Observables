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

    [SseEvent]
    Observable<string> Heartbeats { get; }
}
