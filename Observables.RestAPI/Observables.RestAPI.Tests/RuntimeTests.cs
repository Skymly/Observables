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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        User received = await api.GetUserObservable(7).FirstAsync(cts.Token);

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
        Observable<User> GetUserObservable(int id);
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
