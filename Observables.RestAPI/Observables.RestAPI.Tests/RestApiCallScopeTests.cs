using System.Collections;
using System.Net;
using System.Text;
using R3;

namespace Observables.RestAPI.Tests;

public sealed class RestApiCallScopeTests
{
    [Fact]
    public void Push_copies_options_and_nested_dispose_restores_the_outer_scope()
    {
        Assert.Null(RestApiCallScope.Capture());

        var input = new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 };
        using (RestApiCallScope.Push(input))
        {
            input["a"] = 9;
            input["z"] = "mutated";
            var outer = RestApiCallScope.Capture();
            Assert.NotNull(outer);
            Assert.Equal(1, outer["a"]);
            Assert.False(outer.ContainsKey("z"));

            using (RestApiCallScope.Push(new Dictionary<string, object?> { ["b"] = 3, ["c"] = 4 }))
            {
                var inner = RestApiCallScope.Capture();
                Assert.NotNull(inner);
                Assert.Equal(1, inner["a"]);
                Assert.Equal(3, inner["b"]);
                Assert.Equal(4, inner["c"]);
                Assert.Equal(2, outer["b"]);
            }

            Assert.Equal(2, RestApiCallScope.Capture()!["b"]);
            Assert.False(RestApiCallScope.Capture()!.ContainsKey("c"));
        }

        Assert.Null(RestApiCallScope.Capture());
    }

    [Fact]
    public void Empty_push_is_a_current_scope_and_double_dispose_does_not_clobber_a_later_one()
    {
        using (RestApiCallScope.Push(new Dictionary<string, object?>()))
        {
            var captured = RestApiCallScope.Capture();
            Assert.NotNull(captured);
            Assert.Empty(captured);
        }

        var first = RestApiCallScope.Push(new Dictionary<string, object?> { ["a"] = 1 });
        first.Dispose();
        first.Dispose();
        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["a"] = 2 }))
        {
            first.Dispose();
            Assert.Equal(2, RestApiCallScope.Capture()!["a"]);
        }

        Assert.Null(RestApiCallScope.Capture());
    }

    [Fact]
    public void Push_rejects_null_options_and_null_keys()
    {
        Assert.Throws<ArgumentNullException>(() => RestApiCallScope.Push(null!));
        Assert.Throws<ArgumentException>(() => RestApiCallScope.Push(new NullKeyOptions()));
        Assert.Null(RestApiCallScope.Capture());
    }

    [Fact]
    public void Bind_applies_settings_then_snapshot_then_property()
    {
        var settings = new RestApiSettings
        {
            HttpRequestMessageOptions = new Dictionary<string, object>
            {
                ["tenant"] = "settings",
                ["region"] = "us",
            },
        };
        var spec = new RestApiBridge.MethodSpec(
            "GET",
            "/users",
            [new RestApiBridge.Binding(RestApiBridge.SlotKind.Property, 0, "tenant")]);
        var snapshot = new Dictionary<string, object?>
        {
            ["tenant"] = "scope",
            ["trace"] = "abc",
        };

        var bound = RestApiProtocol.Bind(settings, spec, ["property"], snapshot);

        Assert.Equal("property", Option(bound.Message, "tenant"));
        Assert.Equal("us", Option(bound.Message, "region"));
        Assert.Equal("abc", Option(bound.Message, "trace"));
        bound.Message.Dispose();
    }

    [Fact]
    public void Bind_lets_a_null_snapshot_value_override_the_settings_option()
    {
        var settings = new RestApiSettings
        {
            HttpRequestMessageOptions = new Dictionary<string, object> { ["tenant"] = "acme" },
        };
        var spec = new RestApiBridge.MethodSpec("GET", "/users", []);

        var bound = RestApiProtocol.Bind(
            settings,
            spec,
            [],
            new Dictionary<string, object?> { ["tenant"] = null });

        // HttpRequestOptions.TryGetValue does not surface a stored null, so a null snapshot
        // value clears the settings default for a handler reading Options.
        Assert.False(bound.Message.Options.TryGetValue(new HttpRequestOptionsKey<object>("tenant"), out _));
        bound.Message.Dispose();
    }

    [Fact]
    public void Bind_does_not_read_the_ambient_scope()
    {
        var spec = new RestApiBridge.MethodSpec("GET", "/users", []);
        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "leak" }))
        {
            var bound = RestApiProtocol.Bind(new RestApiSettings(), spec, []);
            Assert.False(HasOption(bound.Message, "stamp"));
            bound.Message.Dispose();
        }
    }

    [Fact]
    public async Task Overlapping_calls_keep_the_scope_current_at_each_proxy_entry()
    {
        var handler = new HoldingHandler();
        using var client = Client(handler);
        var settings = new RestApiSettings
        {
            HttpRequestMessageOptions = new Dictionary<string, object> { ["tenant"] = "acme" },
        };
        var api = RestService.For<IScopedApi>(client, settings);

        Task first;
        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["page"] = 1, ["tenant"] = "one" }))
            first = api.GetTask(1);
        await handler.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Task second;
        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["page"] = 2 }))
            second = api.GetTask(2);
        try
        {
            await handler.SecondEntered.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var seen = handler.Seen;
            Assert.Equal(2, seen.Count);
            var page1 = seen.Single(item => item.Path.EndsWith("/1", StringComparison.Ordinal));
            var page2 = seen.Single(item => item.Path.EndsWith("/2", StringComparison.Ordinal));
            Assert.Equal(1, page1.Options["page"]);
            Assert.Equal("one", page1.Options["tenant"]);
            Assert.Equal(2, page2.Options["page"]);
            Assert.Equal("acme", page2.Options["tenant"]);
        }
        finally
        {
            handler.Release();
            await Task.WhenAll(first, second);
        }
    }

    [Fact]
    public async Task Task_ValueTask_and_sync_proxies_stamp_the_entry_scope()
    {
        var handler = new RecordingHandler();
        using var client = Client(handler);
        var api = RestService.For<IScopedApi>(client);

        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "task" }))
            await api.GetTask(1);
        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "value" }))
            await api.GetValue(2);
        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "sync" }))
            api.GetSync(3);

        Assert.Equal("task", Stamp(handler, "/1"));
        Assert.Equal("value", Stamp(handler, "/2"));
        Assert.Equal("sync", Stamp(handler, "/3"));
    }

    [Fact]
    public async Task Property_parameter_wins_over_the_entry_snapshot()
    {
        var handler = new RecordingHandler();
        using var client = Client(handler);
        var api = RestService.For<IScopedApi>(client);

        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = 1, ["tenant"] = "scope" }))
            await api.GetWithProperty(7, 99);

        var seen = handler.Seen.Single();
        Assert.Equal(99, seen.Options["stamp"]);
        Assert.Equal("scope", seen.Options["tenant"]);
    }

    [Fact]
    public async Task Cold_observable_keeps_the_entry_snapshot_after_the_scope_is_disposed()
    {
        var handler = new RecordingHandler();
        using var client = Client(handler);
        var api = RestService.For<IScopedApi>(client);

        Observable<Item> cold;
        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "frozen" }))
            cold = api.GetObservable(6);

        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "later" }))
        {
            await cold.FirstAsync(TestContext.Current.CancellationToken);
            await api.GetTask(8);
        }

        await api.GetTask(9);

        Assert.Equal("frozen", Stamp(handler, "/6"));
        Assert.Equal("later", Stamp(handler, "/8"));
        Assert.False(handler.Seen.Single(item => item.Path.EndsWith("/9", StringComparison.Ordinal)).Options.ContainsKey("stamp"));
    }

    [Fact]
    public async Task Call_entered_with_no_scope_does_not_see_another_flows_options()
    {
        var handler = new RecordingHandler();
        using var client = Client(handler);
        var api = RestService.For<IScopedApi>(client);
        var ct = TestContext.Current.CancellationToken;
        var otherEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOther = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var other = Task.Run(async () =>
        {
            using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "other" }))
            {
                otherEntered.TrySetResult();
                await releaseOther.Task.WaitAsync(ct);
                await api.GetTask(1);
            }
        }, ct);

        await otherEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await api.GetTask(2);
        releaseOther.TrySetResult();
        await other;

        Assert.False(handler.Seen.Single(item => item.Path.EndsWith("/2", StringComparison.Ordinal)).Options.ContainsKey("stamp"));
        Assert.Equal("other", Stamp(handler, "/1"));
    }

    [Fact]
    public async Task Concurrent_pushes_do_not_cross_wires()
    {
        var handler = new RecordingHandler();
        using var client = Client(handler);
        var api = RestService.For<IScopedApi>(client);
        var ct = TestContext.Current.CancellationToken;

        await Task.WhenAll(
            Task.Run(async () =>
            {
                using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = 11 }))
                    await api.GetTask(11);
            }, ct),
            Task.Run(async () =>
            {
                using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = 22 }))
                    await api.GetTask(22);
            }, ct));

        Assert.Equal(11, (int)handler.Seen.Single(item => item.Path.EndsWith("/11", StringComparison.Ordinal)).Options["stamp"]!);
        Assert.Equal(22, (int)handler.Seen.Single(item => item.Path.EndsWith("/22", StringComparison.Ordinal)).Options["stamp"]!);
    }

    static HttpClient Client(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return client;
    }

    static string Stamp(RecordingHandler handler, string suffix) =>
        (string)handler.Seen.Single(item => item.Path.EndsWith(suffix, StringComparison.Ordinal)).Options["stamp"]!;

    static object? Option(HttpRequestMessage request, string key)
    {
        Assert.True(request.Options.TryGetValue(new HttpRequestOptionsKey<object>(key), out var value));
        return value;
    }

    static bool HasOption(HttpRequestMessage request, string key) =>
        request.Options.TryGetValue(new HttpRequestOptionsKey<object>(key), out _);

    public interface IScopedApi
    {
        [Get("/items/{id}")]
        Task<Item> GetTask(int id);

        [Get("/items/{id}")]
        ValueTask<Item> GetValue(int id);

        [Get("/items/{id}")]
        Item GetSync(int id);

        [Get("/items/{id}")]
        Observable<Item> GetObservable(int id);

        [Get("/items/{id}")]
        Task<Item> GetWithProperty(int id, [Property("stamp")] int stamp);
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    sealed record CapturedRequest(string Path, Dictionary<string, object?> Options)
    {
        public static CapturedRequest From(HttpRequestMessage request)
        {
            var options = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var key in new[] { "stamp", "tenant", "page", "trace", "region" })
            {
                if (request.Options.TryGetValue(new HttpRequestOptionsKey<object>(key), out var value))
                    options[key] = value;
            }

            return new(request.RequestUri?.AbsolutePath ?? "", options);
        }
    }

    sealed class RecordingHandler : HttpMessageHandler
    {
        readonly List<CapturedRequest> _seen = new();

        public IReadOnlyList<CapturedRequest> Seen
        {
            get
            {
                lock (_seen)
                    return _seen.ToArray();
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (_seen)
                _seen.Add(CapturedRequest.From(request));
            return Task.FromResult(Ok());
        }
    }

    sealed class HoldingHandler : HttpMessageHandler
    {
        readonly object _gate = new();
        readonly List<CapturedRequest> _seen = new();
        readonly TaskCompletionSource _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource _second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstEntered => _first.Task;
        public Task SecondEntered => _second.Task;

        public IReadOnlyList<CapturedRequest> Seen
        {
            get
            {
                lock (_gate)
                    return _seen.ToArray();
            }
        }

        public void Release() => _release.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var captured = CapturedRequest.From(request);
            int count;
            lock (_gate)
            {
                _seen.Add(captured);
                count = _seen.Count;
            }

            if (count == 1)
                _first.TrySetResult();
            if (count >= 2)
                _second.TrySetResult();

            await _release.Task.WaitAsync(cancellationToken);
            return Ok();
        }
    }

    static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":1,"name":"Ada"}""", Encoding.UTF8, "application/json"),
        };

    sealed class NullKeyOptions : IReadOnlyDictionary<string, object?>
    {
        public object? this[string key] => null;
        public IEnumerable<string> Keys => new string[] { null! };
        public IEnumerable<object?> Values => new object?[] { 1 };
        public int Count => 1;
        public bool ContainsKey(string key) => false;
        public bool TryGetValue(string key, out object? value)
        {
            value = null;
            return false;
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object?>(null!, 1);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
