using System.Net;
using Observables.RestAPI;
using RichardSzalay.MockHttp;
using R3;

var mockHttp = new MockHttpMessageHandler();
mockHttp.When(HttpMethod.Get, "https://api.example.com/ping")
    .Respond(HttpStatusCode.OK, "application/json", """{"ok":true}""");

using HttpClient client = mockHttp.ToHttpClient();
client.BaseAddress = new Uri("https://api.example.com");

var api = RestService.For<IPingApi>(client);
using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
PingResponse response = await api.Ping().FirstAsync(cts.Token);

if (!response.Ok)
{
    throw new InvalidOperationException("Unexpected ping response.");
}

Console.WriteLine("Observables.RestAPI.R3 consumer smoke OK");

public interface IPingApi
{
    [Get("/ping")]
    Observable<PingResponse> Ping();
}

public sealed class PingResponse
{
    public bool Ok { get; set; }
}
