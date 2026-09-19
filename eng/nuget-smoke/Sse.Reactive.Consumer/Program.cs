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
    public static void Main()
    {
        using HttpClient http = new();
        SseConnection connection = new(http, new Uri("https://example.invalid/events"));
        ISmokeFeed hub = SseService.For<ISmokeFeed>(connection);
        if (hub is null)
        {
            throw new InvalidOperationException("Sse Reactive generated proxy was not created.");
        }

        Console.WriteLine("Observables.Sse.Reactive consumer smoke OK");
    }
}
