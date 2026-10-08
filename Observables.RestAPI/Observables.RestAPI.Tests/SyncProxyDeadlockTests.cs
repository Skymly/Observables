using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace Observables.RestAPI.Tests;

public sealed class SyncProxyDeadlockTests
{
    [Fact]
    public void Sync_result_proxy_finishes_when_exception_factory_yields()
    {
        using var client = CreateClient("pong");
        var api = RestService.For<ISyncProbeApi>(client, Settings(YieldingNull));

        using var context = new SingleThreadSynchronizationContext();
        string? value = null;
        var finished = context.TryInvoke(() => value = api.Ping(), TimeSpan.FromSeconds(2), out var error);

        Assert.True(finished, "TIMED_OUT_DEADLOCK");
        Assert.Null(error);
        Assert.Equal("pong", value);
        AssertClientUsable(client);
    }

    [Fact]
    public void Sync_void_proxy_finishes_when_exception_factory_yields()
    {
        using var client = CreateClient("pong");
        var api = RestService.For<ISyncProbeApi>(client, Settings(YieldingNull));

        using var context = new SingleThreadSynchronizationContext();
        var finished = context.TryInvoke(() => api.Delete(), TimeSpan.FromSeconds(2), out var error);

        Assert.True(finished, "TIMED_OUT_DEADLOCK");
        Assert.Null(error);
        AssertClientUsable(client);
    }

    [Fact]
    public void Sync_result_proxy_surfaces_exception_factory_exception()
    {
        using var client = CreateClient("pong");
        var api = RestService.For<ISyncProbeApi>(client, Settings(YieldingError));

        using var context = new SingleThreadSynchronizationContext();
        var finished = context.TryInvoke(() => api.Ping(), TimeSpan.FromSeconds(2), out var error);

        Assert.True(finished, "TIMED_OUT_DEADLOCK");
        var thrown = Assert.IsType<InvalidOperationException>(error);
        Assert.Equal("yielded", thrown.Message);
        AssertClientUsable(client);
    }

    [Fact]
    public void Sync_result_proxy_finishes_when_serializer_yields()
    {
        using var client = CreateClient("{\"Id\":7}");
        var settings = new RestApiSettings
        {
            ExceptionFactory = static _ => Task.FromResult<Exception?>(null),
            ContentSerializer = new YieldingSerializer(),
        };
        var api = RestService.For<ISyncProbeApi>(client, settings);

        using var context = new SingleThreadSynchronizationContext();
        ProbeDto? value = null;
        var finished = context.TryInvoke(() => value = api.PingDto(), TimeSpan.FromSeconds(2), out var error);

        Assert.True(finished, "TIMED_OUT_DEADLOCK");
        Assert.Null(error);
        Assert.NotNull(value);
        Assert.Equal(7, value.Id);
        AssertClientUsable(client);
    }

    [Fact]
    public void Async_proxy_still_observes_caller_context_when_exception_factory_yields()
    {
        using var client = CreateClient("pong");
        SynchronizationContext? seen = null;
        var settings = new RestApiSettings
        {
            ExceptionFactory = async _ =>
            {
                seen = SynchronizationContext.Current;
                await Task.Yield();
                return null;
            },
        };
        var api = RestService.For<ISyncProbeApi>(client, settings);

        using var context = new SingleThreadSynchronizationContext();
        string? value = null;
        var finished = context.TryInvokeAsync(
            async () => value = await api.PingAsync(),
            TimeSpan.FromSeconds(2),
            out var error);

        Assert.True(finished, "TIMED_OUT_DEADLOCK");
        Assert.Null(error);
        Assert.Equal("pong", value);
        Assert.Same(context, seen);
        AssertClientUsable(client);
    }

    static RestApiSettings Settings(Func<HttpResponseMessage, Task<Exception?>> factory) =>
        new() { ExceptionFactory = factory };

    static async Task<Exception?> YieldingNull(HttpResponseMessage response)
    {
        _ = response;
        await Task.Yield();
        return null;
    }

    static async Task<Exception?> YieldingError(HttpResponseMessage response)
    {
        _ = response;
        await Task.Yield();
        return new InvalidOperationException("yielded");
    }

    static HttpClient CreateClient(string body)
    {
        var client = new HttpClient(new OkHandler(body))
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        return client;
    }

    static void AssertClientUsable(HttpClient client)
    {
        using var response = client
            .SendAsync(new HttpRequestMessage(HttpMethod.Get, "/ping"))
            .GetAwaiter()
            .GetResult();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public interface ISyncProbeApi
    {
        [Get("/ping")]
        string Ping();

        [Get("/ping")]
        Task<string> PingAsync();

        [Get("/ping")]
        ProbeDto PingDto();

        [Delete("/ping")]
        void Delete();
    }

    public sealed class ProbeDto
    {
        public int Id { get; set; }
    }

    sealed class YieldingSerializer : IHttpContentSerializer
    {
        public HttpContent ToHttpContent<T>(T item) => new StringContent(string.Empty);

        public async Task<T?> FromHttpContentAsync<T>(
            HttpContent content,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var json = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json);
        }

        public string? GetFieldNameForProperty(PropertyInfo propertyInfo) => propertyInfo.Name;
    }

    sealed class OkHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            };
            return Task.FromResult(response);
        }
    }

    sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
    {
        readonly BlockingCollection<Action> queue = new();
        readonly Thread thread;
        readonly ManualResetEventSlim ready = new(false);

        public SingleThreadSynchronizationContext()
        {
            thread = new Thread(Pump)
            {
                IsBackground = true,
                Name = "single-thread-sync-context",
            };
            thread.Start();
            ready.Wait();
        }

        public override void Post(SendOrPostCallback callback, object? state) =>
            queue.Add(() => callback(state));

        public bool TryInvoke(Action action, TimeSpan timeout, out Exception? error) =>
            TryInvokeAsync(
                () =>
                {
                    action();
                    return Task.CompletedTask;
                },
                timeout,
                out error);

        public bool TryInvokeAsync(Func<Task> action, TimeSpan timeout, out Exception? error)
        {
            Exception? captured = null;
            using var done = new ManualResetEventSlim(false);
            Post(
                async _ =>
                {
                    try
                    {
                        await action().ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        captured = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                },
                null);

            var finished = done.Wait(timeout);
            error = captured;
            return finished;
        }

        public void Dispose()
        {
            queue.CompleteAdding();
            ready.Dispose();
        }

        void Pump()
        {
            SetSynchronizationContext(this);
            ready.Set();
            foreach (var action in queue.GetConsumingEnumerable())
                action();
        }
    }
}
