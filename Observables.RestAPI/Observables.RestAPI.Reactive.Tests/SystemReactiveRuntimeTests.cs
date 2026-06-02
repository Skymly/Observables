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

    public interface IIoUserApi
    {
        [Get("/users/{id}")]
        IObservable<User> GetUser(int id);
    }

    public sealed class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
