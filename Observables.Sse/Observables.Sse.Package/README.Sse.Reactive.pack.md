# Observables.Sse.Reactive

Declarative Server-Sent Events (SSE) client proxies with Roslyn source generators — annotate interfaces with `[SseEvent]` to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies for text/event-stream realtime feeds.

## Install

```xml
<PackageReference Include="Observables.Sse.Reactive" Version="0.1.1" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

```csharp
using Observables.Sse;
using System;

[Sse]
public interface IPriceFeed
{
    [SseEvent("price")]
    IObservable<string> Prices { get; }

    [SseEvent]
    IObservable<string> Heartbeats { get; }
}

var conn = new SseConnection(new HttpClient(), new Uri("https://example.com/stream"));
var feed = SseService.For<IPriceFeed>(conn);
feed.Prices.Subscribe(Console.WriteLine);
```
