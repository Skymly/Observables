using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Observables.Sse.Tests.Infrastructure;

/// <summary>Delays HTTP headers until <paramref name="delay"/> elapses or the request is canceled.</summary>
public sealed class DelayedHeadersSseHandler(TimeSpan delay) : HttpMessageHandler
{
    static readonly byte[] Body = Encoding.UTF8.GetBytes(
        "event: price\n" +
        "data: 1\n" +
        "\n");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Body),
            RequestMessage = request
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return response;
    }
}

/// <summary>Returns headers immediately, then delays the body.</summary>
public sealed class SlowBodySseHandler(TimeSpan bodyDelay) : HttpMessageHandler
{
    static readonly byte[] Body = Encoding.UTF8.GetBytes(
        "event: price\n" +
        "data: late\n" +
        "\n");

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var stream = new DelayedStream(bodyDelay, Body);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream),
            RequestMessage = request
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return Task.FromResult(response);
    }

    sealed class DelayedStream(TimeSpan delay, byte[] payload) : Stream
    {
        int _offset;
        bool _delayed;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!_delayed)
            {
                _delayed = true;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            if (_offset >= payload.Length)
            {
                return 0;
            }

            var n = Math.Min(buffer.Length, payload.Length - _offset);
            payload.AsSpan(_offset, n).CopyTo(buffer.Span);
            _offset += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
