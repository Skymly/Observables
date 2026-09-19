using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Observables.RestAPI;
using RichardSzalay.MockHttp;

namespace Observables.RestAPI.Reactive.Tests;

public sealed class SystemReactiveRuntimeTests
{
    [Fact]
    public async Task IObservableGet_deserializes_response()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/3")
            .Respond(HttpStatusCode.OK, "application/json", """{"id":3,"name":"Lin"}""");

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");

        var api = RestService.For<IIoUserApi>(client);
        User user = await api.GetUser(3).FirstAsync().ToTask();

        Assert.Equal(3, user.Id);
        Assert.Equal("Lin", user.Name);
    }

    [Fact]
    public async Task IObservableGet_throws_ApiException_on_404()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/404")
            .Respond(HttpStatusCode.NotFound);

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");

        var api = RestService.For<IIoUserApi>(client);
        await Assert.ThrowsAsync<ApiException>(() => api.GetUser(404).FirstAsync().ToTask());
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.ResetContent)]
    [InlineData(HttpStatusCode.OK)]
    public async Task IObservableDelete_unit_succeeds_on_empty_success(HttpStatusCode status)
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Delete, "https://api.example.com/users/1")
            .Respond(status);

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");
        var api = RestService.For<IIoUserApi>(client);
        await api.DeleteUser(1).FirstAsync().ToTask();
    }
    public interface IIoUserApi
    {
        [Get("/users/{id}")]
        IObservable<User> GetUser(int id);

        [Delete("/users/{id}")]
        IObservable<System.Reactive.Unit> DeleteUser(int id);
    }

    public sealed class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
