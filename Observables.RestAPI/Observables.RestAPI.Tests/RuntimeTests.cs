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
        User user = await api.GetUser(42, TestContext.Current.CancellationToken);

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
        User received = await api.GetUserObservable(7, TestContext.Current.CancellationToken)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(7, received.Id);
    }

    [Fact]
    public async Task TaskGet_throws_ApiException_on_404()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/404")
            .Respond(HttpStatusCode.NotFound);

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");

        var api = RestService.For<IUserApi>(client);
        await Assert.ThrowsAsync<ApiException>(
            () => api.GetUser(404, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IApiResponse_does_not_dispose_content_until_caller_disposes()
    {
        using var handler = new TrackingJsonHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IUserApi>(client);

        IApiResponse<User> response = await api.GetUserResponse(1, TestContext.Current.CancellationToken);

        Assert.False(handler.Content.IsDisposed);
        Assert.True(response.IsSuccessful);
        Assert.Equal("Ada", response.Content!.Name);
        Assert.NotNull(response.Headers);
        Assert.Equal("application/json", response.ContentHeaders?.ContentType?.MediaType);

        response.Dispose();
        Assert.True(handler.Content.IsDisposed);
    }

    [Fact]
    public async Task IApiResponse_caller_cancel_throws_OperationCanceledException()
    {
        using var handler = new StallUntilCanceledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IUserApi>(client);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var task = api.GetUserResponse(1, cts.Token);
        await handler.Started;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task TaskGet_caller_cancel_throws_OperationCanceledException()
    {
        using var handler = new StallUntilCanceledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IUserApi>(client);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var task = api.GetUser(1, cts.Token);
        await handler.Started;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task TaskGet_timeout_like_cancel_throws_ApiRequestException()
    {
        using var handler = new TimeoutLikeHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IUserApi>(client);

        var ex = await Assert.ThrowsAsync<ApiRequestException>(
            () => api.GetUser(1, TestContext.Current.CancellationToken));
        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    [Fact]
    public async Task TaskDelete_timeout_like_cancel_throws_ApiRequestException()
    {
        using var handler = new TimeoutLikeHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IUserApi>(client);

        var ex = await Assert.ThrowsAsync<ApiRequestException>(
            () => api.DeleteUser(1, TestContext.Current.CancellationToken));
        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    [Fact]
    public async Task For_hostUrl_keeps_base_path()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/api/v1/users/42")
            .Respond(HttpStatusCode.OK, "application/json", """{"id":42,"name":"Ada"}""");

        var settings = new RestApiSettings
        {
            HttpMessageHandlerFactory = () => mockHttp,
        };
        var api = RestService.For<IUserApi>("https://api.example.com/api/v1", settings);
        User user = await api.GetUser(42, TestContext.Current.CancellationToken);

        Assert.Equal(42, user.Id);
    }

    [Fact]
    public async Task Path_AliasAs_binds_placeholder()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/42")
            .Respond(HttpStatusCode.OK, "application/json", """{"id":42,"name":"Ada"}""");

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");
        var api = RestService.For<IAliasedUserApi>(client);

        User user = await api.GetUser(42, TestContext.Current.CancellationToken);
        Assert.Equal(42, user.Id);
    }

    [Fact]
    public async Task For_closed_generic_interface_creates_client()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/items/7")
            .Respond(HttpStatusCode.OK, "application/json", """{"id":7,"name":"Ada"}""");

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");
        var api = RestService.For<IGenericApi<User>>(client);

        User user = await api.Get(7, TestContext.Current.CancellationToken);
        Assert.Equal(7, user.Id);
    }

    [Fact]
    public async Task IsReceived_does_not_imply_Content()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/1")
            .Respond(HttpStatusCode.OK, "application/json", "null");

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");
        var api = RestService.For<IUserApi>(client);

        IApiResponse<User> response = await api.GetUserResponse(1, TestContext.Current.CancellationToken);

        Assert.True(response.IsReceived);
        Assert.Null(response.Content);
    }

    [Fact]
    public async Task IApiResponse_timeout_like_cancel_stores_ApiRequestException()
    {
        using var handler = new TimeoutLikeHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IUserApi>(client);

        IApiResponse<User> response = await api.GetUserResponse(1, TestContext.Current.CancellationToken);

        Assert.False(response.IsSuccessful);
        Assert.True(response.HasRequestError(out var error));
        Assert.IsType<TaskCanceledException>(error.InnerException);
    }

    [Fact]
    public async Task IApiResponse_plain_ExceptionFactory_error_is_coerced()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/users/1")
            .Respond(HttpStatusCode.OK, "application/json", """{"id":1,"name":"Ada"}""");

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");
        var settings = new RestApiSettings
        {
            ExceptionFactory = _ => Task.FromResult<Exception?>(new InvalidOperationException("nope")),
        };
        var api = RestService.For<IUserApi>(client, settings);

        IApiResponse<User> response = await api.GetUserResponse(1, TestContext.Current.CancellationToken);

        Assert.False(response.IsSuccessful);
        Assert.IsType<ApiException>(response.Error);
        Assert.IsType<InvalidOperationException>(response.Error!.InnerException);
        Assert.Equal("nope", response.Error.InnerException!.Message);
    }

    [Fact]
    public void For_without_generated_factory_throws()
    {
        using var client = new HttpClient();
        var ex = Assert.Throws<InvalidOperationException>(() => RestService.For<INotGeneratedApi>(client));
        Assert.Contains("does not have a generated REST API client", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Query_values_are_uri_escaped_by_default()
    {
        string? requestUri = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://api.example.com/search*")
            .Respond(req =>
            {
                requestUri = req.RequestUri!.OriginalString;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("\"ok\"", Encoding.UTF8, "application/json"),
                };
            });

        var client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri("https://api.example.com");
        var api = RestService.For<IUserApi>(client);

        await api.Search("a&b=c #", TestContext.Current.CancellationToken);

        Assert.NotNull(requestUri);
        Assert.Contains("q=a%26b%3Dc%20%23", requestUri, StringComparison.Ordinal);
        Assert.DoesNotContain("q=a&b=c", requestUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_disposes_request_when_not_returning_IApiResponse()
    {
        using var handler = new TrackingJsonHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var content = new TrackingContent();
        var request = new HttpRequestMessage(HttpMethod.Get, "/users/1") { Content = content };

        await RestApiBridge.SendAsync<User, User>(
            client,
            request,
            new RestApiSettings(),
            bodyBuffered: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(content.IsDisposed);
    }

    [Fact]
    public async Task SendAsync_does_not_dispose_request_for_IApiResponse()
    {
        using var handler = new TrackingJsonHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var content = new TrackingContent();
        var request = new HttpRequestMessage(HttpMethod.Get, "/users/1") { Content = content };

        var response = await RestApiBridge.SendAsync<IApiResponse<User>, User>(
            client,
            request,
            new RestApiSettings(),
            bodyBuffered: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(content.IsDisposed);
        response!.Dispose();
    }

    [Fact]
    public async Task IApiResponse_Dispose_disposes_the_request()
    {
        using var handler = new TrackingJsonHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var requestContent = new TrackingContent();
        var request = new HttpRequestMessage(HttpMethod.Get, "/users/1") { Content = requestContent };

        var response = await RestApiBridge.SendAsync<IApiResponse<User>, User>(
            client,
            request,
            new RestApiSettings(),
            bodyBuffered: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(requestContent.IsDisposed);
        response!.Dispose();
        Assert.True(requestContent.IsDisposed);
    }

    [Fact]
    public async Task ObservableGet_caller_cancel_throws_OperationCanceledException()
    {
        using var handler = new StallUntilCanceledHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IUserApi>(client);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var consume = api.GetUserObservable(1, cts.Token).FirstAsync(TestContext.Current.CancellationToken);
        await handler.Started.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => consume.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AuthorizationHeaderValueGetter_sets_header_when_request_has_none()
    {
        var handler = new CaptureJsonHandler();
        var settings = new RestApiSettings
        {
            HttpMessageHandlerFactory = () => handler,
            AuthorizationHeaderValueGetter = (_, _) => Task.FromResult("tok-123"),
        };

        var api = RestService.For<IUserApi>("https://api.example.com", settings);
        await api.GetUser(1, TestContext.Current.CancellationToken);

        Assert.NotNull(handler.Request);
        Assert.NotNull(handler.Request.Headers.Authorization);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization.Scheme);
        Assert.Equal("tok-123", handler.Request.Headers.Authorization.Parameter);
    }

    [Fact]
    public void Dispose_does_not_dispose_external_HttpClient()
    {
        using var handler = new TrackingJsonHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IDisposableUserApi>(client);

        Assert.False(RestService.OwnsHttpClient(client));
        api.Dispose();
        Assert.NotNull(client.BaseAddress);
    }

    [Fact]
    public void Dispose_disposes_hostUrl_created_HttpClient()
    {
        var api = RestService.For<IDisposableUserApi>("https://api.example.com");
        var client = (HttpClient)api.GetType().GetProperty("Client")!.GetValue(api)!;

        Assert.True(RestService.OwnsHttpClient(client));
        api.Dispose();
        Assert.Throws<ObjectDisposedException>(() => client.Timeout = TimeSpan.FromSeconds(1));
    }

    public interface INotGeneratedApi
    {
        Task<int> Ping();
    }

    public interface IUserApi
    {
        [Get("/users/{id}")]
        Task<User> GetUser(int id, CancellationToken cancellationToken = default);

        [Get("/users/{id}")]
        Task<IApiResponse<User>> GetUserResponse(int id, CancellationToken cancellationToken = default);

        [Get("/users/{id}")]
        Observable<User> GetUserObservable(int id, CancellationToken cancellationToken = default);

        [Delete("/users/{id}")]
        Task DeleteUser(int id, CancellationToken cancellationToken = default);

        [Get("/search")]
        Task<string> Search([Query] string q, CancellationToken cancellationToken = default);
    }

    public interface IAliasedUserApi
    {
        [Get("/users/{id}")]
        Task<User> GetUser([AliasAs("id")] int userId, CancellationToken cancellationToken = default);
    }

    public interface IGenericApi<T>
    {
        [Get("/items/{id}")]
        Task<T> Get(int id, CancellationToken cancellationToken = default);
    }

    public interface IDisposableUserApi : IDisposable
    {
        [Get("/users/{id}")]
        Task<User> GetUser(int id);
    }

    public sealed class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    sealed class TrackingJsonHandler : HttpMessageHandler
    {
        public TrackingContent Content { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Content,
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }

    sealed class TrackingContent : HttpContent
    {
        static readonly byte[] Json = """{"id":1,"name":"Ada"}"""u8.ToArray();

        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Json, 0, Json.Length);

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken) =>
            stream.WriteAsync(Json, cancellationToken).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = Json.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    sealed class CaptureJsonHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":1,"name":"Ada"}""", Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    sealed class StallUntilCanceledHandler : HttpMessageHandler
    {
        readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    sealed class TimeoutLikeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException());
    }
}
