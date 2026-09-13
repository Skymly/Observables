using System.Net.Http;

namespace Observables.Sse.Tests.Infrastructure;

public sealed class ThrowingSseHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("sse-start-failed");
}
