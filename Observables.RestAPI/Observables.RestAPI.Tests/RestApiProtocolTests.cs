using RichardSzalay.MockHttp;

namespace Observables.RestAPI.Tests;

public sealed class RestApiProtocolTests
{
    [Fact]
    public void Bind_formats_path_without_HttpClient()
    {
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users/{id}",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.Path, 0, "id")]);

        var bound = RestApiProtocol.Bind(new RestApiSettings(), spec, [42]);

        Assert.Equal(HttpMethod.Get, bound.Message.Method);
        Assert.Equal("/users/42", bound.RelativeUri);
        Assert.False(bound.BodyBuffered);
    }

    [Fact]
    public void Bind_preserves_custom_http_method()
    {
        var spec = new RestApiBridge.MethodSpec(
            "PURGE",
            "/cache/{key}",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.Path, 0, "key")]);

        var bound = RestApiProtocol.Bind(new RestApiSettings(), spec, ["abc"]);

        Assert.Equal("PURGE", bound.Message.Method.Method);
        Assert.Equal("/cache/abc", bound.RelativeUri);
    }

    [Fact]
    public void Bind_appends_query()
    {
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/repos/{owner}/{repo}/issues",
            [
                new RestApiBridge.Binding(RestApiBridge.SlotKind.Path, 0, "owner"),
                new RestApiBridge.Binding(RestApiBridge.SlotKind.Path, 1, "repo"),
                new RestApiBridge.Binding(RestApiBridge.SlotKind.Query, 2, "state"),
            ]);

        var bound = RestApiProtocol.Bind(new RestApiSettings(), spec, ["skymly", "observables", "open"]);

        Assert.Equal("/repos/skymly/observables/issues?state=open", bound.RelativeUri);
    }

    [Fact]
    public void Bind_copies_header_collection_with_nullable_values()
    {
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.HeaderCollection, 0, "headers")]);

        var headers = new Dictionary<string, string?>
        {
            ["X-Request-Id"] = "abc",
        };

        var bound = RestApiProtocol.Bind(new RestApiSettings(), spec, [headers]);

        Assert.True(bound.Message.Headers.TryGetValues("X-Request-Id", out var requestIds));
        Assert.Equal("abc", Assert.Single(requestIds));
    }

    [Fact]
    public void Bind_copies_header_collection()
    {
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.HeaderCollection, 0, "headers")]);

        var headers = new Dictionary<string, string>
        {
            ["X-Request-Id"] = "abc",
        };

        var bound = RestApiProtocol.Bind(new RestApiSettings(), spec, [headers]);

        Assert.True(bound.Message.Headers.TryGetValues("X-Request-Id", out var requestIds));
        Assert.Equal("abc", Assert.Single(requestIds));
    }

    [Fact]
    public async Task Send_spec_roundtrips_json()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/42")
            .Respond(System.Net.HttpStatusCode.OK, "application/json", """{"id":42,"name":"Ada"}""");

        using var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");

        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users/{id}",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.Path, 0, "id")]);

        var user = await RestApiBridge.SendAsync<RuntimeTests.User, RuntimeTests.User>(
            client,
            new RestApiSettings(),
            spec,
            TestContext.Current.CancellationToken,
            42);

        Assert.Equal(42, user!.Id);
        Assert.Equal("Ada", user.Name);
    }
}
