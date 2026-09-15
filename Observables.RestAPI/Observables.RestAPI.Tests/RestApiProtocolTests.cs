using System.Net;
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
        Assert.Equal("users/42", bound.RelativeUri);
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
        Assert.Equal("cache/abc", bound.RelativeUri);
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

        Assert.Equal("repos/skymly/observables/issues?state=open", bound.RelativeUri);
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
    public void Bind_seeds_request_options_from_settings()
    {
        var spec = new RestApiBridge.MethodSpec("GET", "/users", []);
        var settings = new RestApiSettings
        {
            HttpRequestMessageOptions = new Dictionary<string, object> { ["tenant"] = "acme" },
        };

        var bound = RestApiProtocol.Bind(settings, spec, []);

        Assert.True(bound.Message.Options.TryGetValue(new HttpRequestOptionsKey<object>("tenant"), out var tenant));
        Assert.Equal("acme", tenant);
    }

    [Fact]
    public void Bind_lets_a_Property_parameter_win_over_the_settings_option()
    {
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.Property, 0, "tenant")]);
        var settings = new RestApiSettings
        {
            HttpRequestMessageOptions = new Dictionary<string, object> { ["tenant"] = "acme" },
        };

        var bound = RestApiProtocol.Bind(settings, spec, ["contoso"]);

        Assert.True(bound.Message.Options.TryGetValue(new HttpRequestOptionsKey<object>("tenant"), out var tenant));
        Assert.Equal("contoso", tenant);
    }

    [Fact]
    public void Bind_applies_the_version_and_version_policy()
    {
        var spec = new RestApiBridge.MethodSpec("GET", "/users", []);
        var settings = new RestApiSettings
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        var bound = RestApiProtocol.Bind(settings, spec, []);

        Assert.Equal(HttpVersion.Version20, bound.Message.Version);
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, bound.Message.VersionPolicy);
    }

    [Fact]
    public void Bind_formats_query_keys_with_the_key_formatter()
    {
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.Query, 0, "UserName")]);
        var settings = new RestApiSettings
        {
            UrlParameterKeyFormatter = new CamelCaseUrlParameterKeyFormatter(),
        };

        var bound = RestApiProtocol.Bind(settings, spec, ["ada"]);

        Assert.Equal("users?userName=ada", bound.RelativeUri);
    }

    [Fact]
    public void Bind_leaves_an_explicit_query_key_alone()
    {
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users",
            [
                new RestApiBridge.Binding(
                    RestApiBridge.SlotKind.Query,
                    0,
                    "UserName",
                    query: new RestApiBridge.QueryOptions().WithExplicitName()),
            ]);
        var settings = new RestApiSettings
        {
            UrlParameterKeyFormatter = new CamelCaseUrlParameterKeyFormatter(),
        };

        var bound = RestApiProtocol.Bind(settings, spec, ["ada"]);

        // [AliasAs("UserName")] is the final wire name; only inferred names go through the formatter.
        Assert.Equal("users?UserName=ada", bound.RelativeUri);
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
