using Observables.Sse;

namespace Observables.NuGetSmoke.Sse.Reactive;

[Sse]
public interface ISmokeFeed
{
    [SseEvent("price")]
    IObservable<string> Prices { get; }

    [SseEvent]
    IObservable<string> Messages { get; }
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Sse.Reactive consumer smoke OK");
}
