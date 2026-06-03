using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Observables.RestAPI;
using RichardSzalay.MockHttp;

var mockHttp = new MockHttpMessageHandler();
mockHttp.When(HttpMethod.Get, "https://api.example.com/ping")
    .Respond(HttpStatusCode.OK, "application/json", """{"ok":true}""");

using HttpClient client = mockHttp.ToHttpClient();
client.BaseAddress = new Uri("https://api.example.com");

var api = RestService.For<IPingApi>(client);
PingResponse response = await api.Ping().FirstAsync().ToTask();

if (!response.Ok)
{
    throw new InvalidOperationException("Unexpected ping response.");
}

Console.WriteLine("Observables.RestAPI.Reactive consumer smoke OK");

public interface IPingApi
{
    [Get("/ping")]
    IObservable<PingResponse> Ping();
}

public sealed class PingResponse
{
    public bool Ok { get; set; }
}
