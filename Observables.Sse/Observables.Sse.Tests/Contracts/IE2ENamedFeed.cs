using Observables.Sse;
using R3;

namespace Observables.Sse.Tests.Contracts;

/// <summary>Named counterpart of <see cref="IE2EFeed"/>; resolved through <c>SseService.For&lt;T&gt;()</c>.</summary>
[Sse("e2e-named")]
public interface IE2ENamedFeed
{
    [SseEvent("price")]
    Observable<string> Prices { get; }
}

/// <summary>Carries a name that no test registers a connection for.</summary>
[Sse("e2e-unregistered")]
public interface IE2EUnregisteredFeed
{
    [SseEvent("price")]
    Observable<string> Prices { get; }
}
