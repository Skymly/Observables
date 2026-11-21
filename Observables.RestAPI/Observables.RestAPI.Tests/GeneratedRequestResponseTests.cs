using System.Net;
using System.Text;
using System.Text.Json;

namespace Observables.RestAPI.Tests;

public sealed class GeneratedRequestResponseTests
{
    [Fact]
    public async Task Generic_results_preserve_the_declared_shape()
    {
        string[] expected = ["alpha", "beta"];
        using var client = new HttpClient(new EchoHandler()) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IGenericResultApi>(client);

        using IApiResponse<List<string>> response = api.GetResponse();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, response.Content);

        using ApiResponse<List<string>> concrete = api.GetConcreteResponse();
        Assert.Equal(HttpStatusCode.OK, concrete.StatusCode);
        Assert.Equal(expected, concrete.Content);

        Assert.Equal(expected, api.GetList());

        using IApiResponse<List<string>> asyncResponse = await api.GetResponseAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected, asyncResponse.Content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Reference_array_body_is_sent_as_one_json_value(int count)
    {
        string[] values = ["alpha", "beta"];
        string[] payload = values[..count];
        using var client = new HttpClient(new EchoHandler()) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IArrayApi>(client);

        string body = await api.Send(payload, TestContext.Current.CancellationToken);

        Assert.Equal(JsonSerializer.Serialize(payload), body);
    }

    public interface IArrayApi
    {
        [Post("/batch")]
        Task<string> Send([Body] string[] values, CancellationToken cancellationToken);
    }

    public interface IGenericResultApi
    {
        [Get("/items")]
        IApiResponse<List<string>> GetResponse();

        [Get("/items")]
        ApiResponse<List<string>> GetConcreteResponse();

        [Get("/items")]
        List<string> GetList();

        [Get("/items")]
        Task<IApiResponse<List<string>>> GetResponseAsync(CancellationToken cancellationToken);
    }

    sealed class EchoHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string response = request.Content is null
                ? """["alpha","beta"]"""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
