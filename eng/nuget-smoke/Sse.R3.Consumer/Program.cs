using Observables.Sse;
using R3;

namespace Observables.NuGetSmoke.Sse.R3;

[Sse]
public interface ISmokeFeed
{
    [SseEvent("price")]
    Observable<string> Prices { get; }

    [SseEvent]
    Observable<string> Messages { get; }
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Sse.R3 consumer smoke OK");
}
