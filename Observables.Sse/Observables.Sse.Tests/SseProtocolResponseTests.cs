using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Observables.Sse.Tests;

public sealed class SseProtocolResponseTests
{
    static readonly Uri Endpoint = new("https://sse.example.com/stream");

    [Theory]
    [InlineData(null)]
    [InlineData("text/html")]
    public async Task Successful_response_with_an_invalid_media_type_is_rejected(string? contentType)
    {
        using var client = new HttpClient(new ResponseHandler(HttpStatusCode.OK, contentType, "data: unexpected\n\n"));
        var values = new List<string>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SseProtocol.SubscribeAsync<string>(
            new SseConnection(client, Endpoint), "message", values.Add, () => { }, TestContext.Current.CancellationToken));

        Assert.Contains("text/event-stream", error.Message, StringComparison.Ordinal);
        Assert.Empty(values);
    }

    [Fact]
    public async Task Event_stream_media_type_allows_case_and_parameters()
    {
        using var client = new HttpClient(new ResponseHandler(HttpStatusCode.OK, "Text/Event-Stream; charset=utf-8", "data: hello\n\n"));
        var values = new List<string>();

        await SseProtocol.SubscribeAsync<string>(
            new SseConnection(client, Endpoint), "message", values.Add, () => { }, TestContext.Current.CancellationToken);

        Assert.Equal("hello", Assert.Single(values));
    }

    [Fact]
    public async Task No_content_completes_without_a_media_type()
    {
        using var client = new HttpClient(new ResponseHandler(HttpStatusCode.NoContent, null, ""));
        var values = new List<string>();
        bool completed = false;

        await SseProtocol.SubscribeAsync<string>(
            new SseConnection(client, Endpoint), "message", values.Add, () => completed = true, TestContext.Current.CancellationToken);

        Assert.True(completed);
        Assert.Empty(values);
    }

    sealed class ResponseHandler(HttpStatusCode status, string? contentType, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            if (contentType is not null)
            {
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }

            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }
}
