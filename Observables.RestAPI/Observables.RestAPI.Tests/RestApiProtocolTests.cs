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


    [Fact]
    public void SafeUnescaped_encodes_hash_so_the_query_does_not_become_a_fragment()
    {
        var query = RestApiBridge.BuildQueryString(
            [new KeyValuePair<string, string?>("q", "a#b")],
            UriFormat.SafeUnescaped);
        Assert.Equal("q=a%23b", query);
        Assert.DoesNotContain("#", query);
    }

    [Fact]
    public void Unescaped_leaves_hash_raw()
    {
        var query = RestApiBridge.BuildQueryString(
            [new KeyValuePair<string, string?>("q", "a#b")],
            UriFormat.Unescaped);
        Assert.Equal("q=a#b", query);
    }

    [Fact]
    public void Bind_routes_content_type_header_onto_the_body()
    {
        var spec = new RestApiBridge.MethodSpec(
            "POST",
            "/items",
            [
                new RestApiBridge.Binding(RestApiBridge.SlotKind.Header, 0, "Content-Type"),
                new RestApiBridge.Binding(RestApiBridge.SlotKind.Body, 1, "body"),
            ]);

        var bound = RestApiProtocol.Bind(new RestApiSettings(), spec, ["application/custom", "payload"]);

        Assert.Equal("application/custom", bound.Message.Content!.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Bind_routes_static_content_type_onto_the_body()
    {
        var spec = new RestApiBridge.MethodSpec(
            "POST",
            "/items",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.Body, 0, "body")],
            new RestApiBridge.MethodFlags(staticHeaders: ["Content-Type: application/custom"]));

        var bound = RestApiProtocol.Bind(new RestApiSettings(), spec, ["payload"]);

        Assert.Equal("application/custom", bound.Message.Content!.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Bind_throws_for_a_malformed_static_header()
    {
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users",
            [],
            new RestApiBridge.MethodFlags(staticHeaders: ["NotAHeader"]));

        var ex = Assert.Throws<InvalidOperationException>(() => RestApiProtocol.Bind(new RestApiSettings(), spec, []));
        Assert.Contains("Name: value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Form_encoding_does_not_hold_the_property_cache_lock_while_formatting()
    {
        var formatter = new BlockingFormFormatter();
        var settings = new RestApiSettings
        {
            FormUrlEncodedParameterFormatter = formatter,
        };
        var spec = new RestApiBridge.MethodSpec(
            "POST",
            "/form",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.Body, 0, "body")],
            new RestApiBridge.MethodFlags(bodySerializationMethod: (int)BodySerializationMethod.UrlEncoded));

        var ct = TestContext.Current.CancellationToken;
        var blocker = Task.Run(() => RestApiProtocol.Bind(settings, spec, [new BlockForm()]), ct);
        var enteredDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!formatter.Entered.IsSet)
        {
            Assert.True(DateTime.UtcNow < enteredDeadline, "blocker did not enter Format.");
            await Task.Delay(10, ct);
        }

        var other = Task.Run(() => RestApiProtocol.Bind(settings, spec, [new OtherForm()]), ct);
        try
        {
            await other.WaitAsync(TimeSpan.FromSeconds(2), ct);
        }
        catch (TimeoutException)
        {
            Assert.Fail("Form encoding held the property cache lock across Format.");
        }

        formatter.Release.Set();
        await blocker.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    sealed class BlockForm
    {
        public string Name { get; set; } = "block";
    }

    sealed class OtherForm
    {
        public string Name { get; set; } = "ok";
    }

    sealed class BlockingFormFormatter : IFormUrlEncodedParameterFormatter
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Release { get; } = new();

        public string? Format(object? value, string? formatString)
        {
            if (value is string s && s == "block")
            {
                Entered.Set();
                if (!Release.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Release was not signaled.");
            }

            return value?.ToString();
        }
    }
}
