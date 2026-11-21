using System.Net;
using System.Net.Http.Headers;
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

    [Theory]
    [InlineData("https://api.example.com:443", true)]
    [InlineData("https://api.example.com:8443", false)]
    public async Task AddRestApiClient_applies_the_configured_origin_to_settings_credentials(
        string authorizationOrigin,
        bool authorized)
    {
        AuthenticationHeaderValue? observedHeader = null;
        var getterCalls = 0;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.Expect(HttpMethod.Get, "https://api.example.com/ping")
            .Respond(request =>
            {
                observedHeader = request.Headers.Authorization;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"message":"pong"}""", System.Text.Encoding.UTF8, "application/json"),
                };
            });
        var services = new ServiceCollection();
        services.AddRestApiClient<IPingApi>(
            client => client.BaseAddress = new Uri("https://api.example.com"),
            settings: new RestApiSettings
            {
                HttpMessageHandlerFactory = () => mockHttp,
                AuthorizationHeaderOrigin = new Uri(authorizationOrigin),
                AuthorizationHeaderValueGetter = (_, _) =>
                {
                    getterCalls++;
                    return Task.FromResult("fixture-token");
                },
            });

        using var provider = services.BuildServiceProvider();
        var response = await provider.GetRequiredService<IPingApi>().Ping();

        Assert.Equal("pong", response.Message);
        mockHttp.VerifyNoOutstandingExpectation();
        Assert.Equal(authorized ? 1 : 0, getterCalls);
        Assert.Equal(authorized ? "Bearer" : null, observedHeader?.Scheme);
        Assert.Equal(authorized ? "fixture-token" : null, observedHeader?.Parameter);
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
