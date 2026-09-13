using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Observables.Sse.Tests.Infrastructure;

/// <summary>
/// Serves one complete SSE event then blocks until the response stream is disposed.
/// </summary>
public sealed class HoldOpenSseHandler : HttpMessageHandler
{
    static readonly byte[] ReadyEvent = Encoding.UTF8.GetBytes(
        "event: price\n" +
        "data: ready\n" +
        "\n");

    readonly TaskCompletionSource _streamDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task StreamDisposed => _streamDisposed.Task;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var stream = new HoldOpenStream(ReadyEvent, _streamDisposed);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream),
            RequestMessage = request
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return Task.FromResult(response);
    }

    sealed class HoldOpenStream : Stream
    {
        readonly byte[] _prefix;
        readonly TaskCompletionSource _disposed;
        readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _offset;

        public HoldOpenStream(byte[] prefix, TaskCompletionSource disposed)
        {
            _prefix = prefix;
            _disposed = disposed;
        }

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
            if (_offset < _prefix.Length)
            {
                var n = Math.Min(buffer.Length, _prefix.Length - _offset);
                _prefix.AsSpan(_offset, n).CopyTo(buffer.Span);
                _offset += n;
                return n;
            }

            await _closed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposed.TrySetResult();
                _closed.TrySetResult();
            }

            base.Dispose(disposing);
        }
    }
}
