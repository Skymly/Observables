using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using Observables.RestAPI;

namespace Observables.RestAPI.Reactive.Tests;

public sealed class RestApiCallScopeReactiveTests
{
    [Fact]
    public async Task Cold_observable_keeps_the_entry_snapshot_after_the_scope_is_disposed()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        var api = RestService.For<IScopedReactiveApi>(client);

        IObservable<Item> cold;
        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "frozen" }))
            cold = api.Get(6);

        using (RestApiCallScope.Push(new Dictionary<string, object?> { ["stamp"] = "later" }))
            await cold.FirstAsync().ToTask();

        await api.GetPlain(9).FirstAsync().ToTask();

        Assert.Equal("frozen", handler.Stamp("/6"));
        Assert.False(handler.Seen.Single(item => item.Path.EndsWith("/9", StringComparison.Ordinal)).Options.ContainsKey("stamp"));
    }

    public interface IScopedReactiveApi
    {
        [Get("/items/{id}")]
        IObservable<Item> Get(int id);

        [Get("/items/{id}")]
        IObservable<Item> GetPlain(int id);
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    sealed record CapturedRequest(string Path, Dictionary<string, object?> Options);

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

        public string Stamp(string suffix) =>
            (string)Seen.Single(item => item.Path.EndsWith(suffix, StringComparison.Ordinal)).Options["stamp"]!;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var options = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (request.Options.TryGetValue(new HttpRequestOptionsKey<object>("stamp"), out var stamp))
                options["stamp"] = stamp;
            lock (_seen)
                _seen.Add(new(request.RequestUri?.AbsolutePath ?? "", options));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":1,"name":"Ada"}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
