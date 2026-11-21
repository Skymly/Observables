using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
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

    [Fact]
    public async Task Exception_factory_failure_releases_both_messages_before_returning_a_wrapper()
    {
        var failure = new InvalidOperationException("exception factory failed");
        var settings = new RestApiSettings { ExceptionFactory = _ => Task.FromException<Exception?>(failure) };
        using var requestBody = new TrackingBody("request");
        using var responseBody = new TrackingBody("response");
        using var client = new HttpClient(new ResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = responseBody,
        }))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        var api = RestService.For<IRequestApi>(client, settings);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => api.GetResponse(requestBody, TestContext.Current.CancellationToken));

        Assert.Same(failure, error);
        Assert.True(requestBody.IsDisposed);
        Assert.True(responseBody.IsDisposed);
    }

    [Fact]
    public async Task Suppressed_http_error_may_leave_the_response_error_null()
    {
        var settings = new RestApiSettings { ExceptionFactory = static _ => Task.FromResult<Exception?>(null) };
        using var requestBody = new StringContent("request");
        using var client = new HttpClient(new ResponseHandler(static _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("suppressed"),
        }))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        var api = RestService.For<IRequestApi>(client, settings);

        using var response = await api.GetResponse(requestBody, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
        Assert.False(response.IsSuccessful);
        Assert.Null(response.Error);
        Assert.Equal("suppressed", response.Content);
    }

    [Theory]
    [InlineData(typeof(IApiResponse))]
    [InlineData(typeof(IApiResponse<string>))]
    [InlineData(typeof(ApiResponse<string>))]
    public void Failure_status_leaves_error_nullable_in_the_public_contract(Type responseType)
    {
        foreach (var statusProperty in new[] { nameof(IApiResponse.IsSuccessStatusCode), nameof(IApiResponse.IsSuccessful) })
        {
            var property = responseType.GetProperty(statusProperty, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.NotNull(property);
            Assert.DoesNotContain(
                property.GetCustomAttributes<MemberNotNullWhenAttribute>(),
                static annotation => !annotation.ReturnValue && annotation.Members.Contains(nameof(IApiResponse.Error)));
        }
    }

    [Fact]
    public async Task Explicit_buffering_sets_content_length_for_result_and_no_result_requests()
    {
        const string body = "hello";
        using var client = new HttpClient(new ResponseHandler(static request =>
        {
            if (request.Content?.Headers.ContentLength != body.Length)
            {
                return new HttpResponseMessage(HttpStatusCode.LengthRequired);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("saved") };
        }))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        var api = RestService.For<IRequestApi>(client);
        var cancellation = TestContext.Current.CancellationToken;
        using var valueBody = new TrackingBody(body);
        using var noResultBody = new TrackingBody(body);
        Assert.Null(valueBody.Headers.ContentLength);
        Assert.Null(noResultBody.Headers.ContentLength);

        Assert.Equal("saved", await api.SaveWithResult(valueBody, cancellation));
        await api.Save(noResultBody, cancellation);
    }

    [Fact]
    public async Task No_result_requests_honor_the_settings_default_and_an_explicit_opt_out()
    {
        var observedLengths = new List<long?>();
        using var client = new HttpClient(new ResponseHandler(request =>
        {
            observedLengths.Add(request.Content?.Headers.ContentLength);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        var api = RestService.For<IRequestApi>(client, new RestApiSettings { Buffered = true });
        var cancellation = TestContext.Current.CancellationToken;
        using var settingsBody = new TrackingBody("hello");
        using var unbufferedBody = new TrackingBody("hello");

        await api.SaveWithSettings(settingsBody, cancellation);
        await api.SaveUnbuffered(unbufferedBody, cancellation);

        Assert.Equal(new long?[] { 5, null }, observedLengths);
    }

    [Fact]
    public async Task ValueTask_and_nongeneric_response_handle_empty_results()
    {
        var count = 0;
        using var client = new HttpClient(new ResponseHandler(_ =>
        {
            count++;
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            response.Headers.Add("X-Receipt", "received");
            return response;
        }))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        var api = RestService.For<INoContentApi>(client);
        var cancellation = TestContext.Current.CancellationToken;

        await api.Send(cancellation);
        using var response = await api.SendForReceipt(cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.IsSuccessful);
        Assert.Null(response.Error);
        Assert.Equal("received", Assert.Single(response.Headers.GetValues("X-Receipt")));
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Nongeneric_response_retains_the_unsuccessful_status_and_error()
    {
        using var client = new HttpClient(new ResponseHandler(static request =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request,
                Content = new StringContent("missing"),
            }))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        var api = RestService.For<INoContentApi>(client);

        using var response = await api.SendForReceipt(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.IsSuccessful);
        Assert.IsType<ApiException>(response.Error);
    }

    [Fact]
    public async Task Derived_headers_apply_to_inherited_methods_with_method_and_interface_precedence()
    {
        var seen = new List<Dictionary<string, string[]>>();
        using var client = new HttpClient(new ResponseHandler(request =>
        {
            seen.Add(request.Headers.ToDictionary(
                header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        var api = RestService.For<IDerivedHeadersApi>(client);
        var cancellation = TestContext.Current.CancellationToken;
        await api.Get(cancellation);
        await api.GetWithMethodHeaders(cancellation);
        await RestService.For<IBaseHeadersApi>(client).Get(cancellation);

        Assert.Equal(new[] { "derived" }, seen[0]["X-Precedence"]);
        Assert.Equal(new[] { "base" }, seen[0]["X-Base"]);
        Assert.Equal(new[] { "middle" }, seen[0]["X-Branch"]);
        Assert.Equal(new[] { "root" }, seen[0]["X-Default"]);
        Assert.DoesNotContain("X-Sibling", seen[0].Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(new[] { "method-first", "method-second" }, seen[1]["X-Precedence"]);
        Assert.Equal(new[] { "root" }, seen[1]["X-Default"]);
        Assert.Equal(new[] { "base" }, seen[2]["X-Precedence"]);
        Assert.DoesNotContain("X-Default", seen[2].Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Named_query_format_replaces_the_constructor_format()
    {
        var paths = new List<string>();
        using var client = new HttpClient(new ResponseHandler(request =>
        {
            paths.Add(request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        var api = RestService.For<IQueryFormatsApi>(client);
        var cancellation = TestContext.Current.CancellationToken;
        await api.Get(3.5m, cancellation);
        await api.GetWithDefaultFormat(3.5m, cancellation);

        Assert.Equal(new[] { "/amount?p-amount=3.50", "/amount?p-amount=3.5" }, paths);
    }

    public interface INoContentApi
    {
        [Post("/no-content")]
        ValueTask Send(CancellationToken cancellationToken);

        [Post("/no-content")]
        Task<IApiResponse> SendForReceipt(CancellationToken cancellationToken);
    }

    [Headers("X-Base: base", "X-Precedence: base")]
    public interface IBaseHeadersApi
    {
        [Get("/base")]
        Task Get(CancellationToken cancellationToken);

        [Get("/base"), Headers("x-PRECEDENCE: method-first", "X-Precedence: method-second")]
        Task GetWithMethodHeaders(CancellationToken cancellationToken);
    }

    [Headers("X-Branch: middle", "x-precedence: middle")]
    public interface IMiddleHeadersApi : IBaseHeadersApi;

    [Headers("X-Sibling: sibling")]
    public interface ISiblingHeaders;

    [Headers("X-Default: root", "X-Precedence: derived")]
    public interface IDerivedHeadersApi : IMiddleHeadersApi, ISiblingHeaders;

    public interface IQueryFormatsApi
    {
        [Get("/amount")]
        Task Get([Query("-", "p", "F0", Format = "F2")] decimal amount, CancellationToken cancellationToken);

        [Get("/amount")]
        Task GetWithDefaultFormat([Query("-", "p", "F0", Format = null)] decimal amount, CancellationToken cancellationToken);
    }

    public interface IRequestApi
    {
        [Post("/save")]
        Task<IApiResponse<string>> GetResponse([Body] HttpContent body, CancellationToken cancellationToken);

        [Post("/save")]
        Task<string> SaveWithResult([Body(buffered: true)] HttpContent body, CancellationToken cancellationToken);

        [Post("/save")]
        Task Save([Body(buffered: true)] HttpContent body, CancellationToken cancellationToken);

        [Post("/save")]
        Task SaveWithSettings([Body] HttpContent body, CancellationToken cancellationToken);

        [Post("/save")]
        Task SaveUnbuffered([Body(buffered: false)] HttpContent body, CancellationToken cancellationToken);
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

    sealed class TrackingBody(string text) : StringContent(text)
    {
        public bool IsDisposed { get; private set; }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed |= disposing;
            base.Dispose(disposing);
        }
    }

    sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
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
