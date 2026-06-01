using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Observables.RestAPI;
using Observables.RestAPI.HttpClientFactory;
using RichardSzalay.MockHttp;

namespace Observables.RestAPI.HttpClientFactory.Tests;

public sealed class HttpClientFactoryIntegrationTests
{
    [Fact]
    public async Task AddRestApiClient_resolves_typed_client()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/ping")
            .Respond(HttpStatusCode.OK, "application/json", """{"message":"pong"}""");

        var services = new ServiceCollection();
        services.AddRestApiClient<IPingApi>(
            client => client.BaseAddress = new Uri("https://api.example.com"),
            settings: new RestApiSettings
            {
                HttpMessageHandlerFactory = () => mockHttp,
            });

        ServiceProvider provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IPingApi>();
        PingResponse response = await api.Ping();

        Assert.Equal("pong", response.Message);
    }

    public interface IPingApi
    {
        [Get("/ping")]
        Task<PingResponse> Ping();
    }

    public sealed class PingResponse
    {
        public string Message { get; set; } = "";
    }
}
