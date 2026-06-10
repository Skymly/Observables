# Observables.Sse.R3

Declarative Server-Sent Events (SSE) proxies for R3 `Observable<T>` (Roslyn-generated proxies).

## Install

```xml
<PackageReference Include="Observables.Sse.R3" Version="0.1.0-preview7" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using Observables.Sse;
using R3;

[Sse]
public interface IPriceFeed
{
    [SseEvent("price")]
    Observable<string> Prices { get; }

    [SseEvent]
    Observable<string> Heartbeats { get; }
}

var conn = new SseConnection(new HttpClient(), new Uri("https://example.com/stream"));
var feed = SseService.For<IPriceFeed>(conn);
feed.Prices.Subscribe(Console.WriteLine);
```
