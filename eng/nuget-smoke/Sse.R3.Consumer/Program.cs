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
    public static void Main()
    {
        using HttpClient http = new();
        SseConnection connection = new(http, new Uri("https://example.invalid/events"));
        ISmokeFeed hub = SseService.For<ISmokeFeed>(connection);
        if (hub is null)
        {
            throw new InvalidOperationException("Sse R3 generated proxy was not created.");
        }

        Console.WriteLine("Observables.Sse.R3 consumer smoke OK");
    }
}
