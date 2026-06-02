using System.Net;
using System.Text;
using System.Text.Json;
using RichardSzalay.MockHttp;
using R3;

namespace Observables.RestAPI.Tests;

public sealed class RuntimeTests
{
    readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public async Task TaskGet_deserializes_response()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/42")
            .Respond(HttpStatusCode.OK, "application/json", """{"id":42,"name":"Ada"}""");

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");

        var api = RestService.For<IUserApi>(client);
        User user = await api.GetUser(42);

        Assert.Equal(42, user.Id);
        Assert.Equal("Ada", user.Name);
    }

    [Fact]
    public async Task ObservableGet_emits_deserialized_value()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/7")
            .Respond(HttpStatusCode.OK, "application/json", """{"id":7,"name":"Grace"}""");

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");

        var api = RestService.For<IUserApi>(client);
        User? received = null;
        using var subscription = api.GetUserObservable(7).Subscribe(user => received = user);

        await Task.Delay(250);
        Assert.NotNull(received);
        Assert.Equal(7, received!.Id);
    }

    public interface IUserApi
    {
        [Get("/users/{id}")]
        Task<User> GetUser(int id);

        [Get("/users/{id}")]
        Observable<User> GetUserObservable(int id);
    }

    public sealed class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
