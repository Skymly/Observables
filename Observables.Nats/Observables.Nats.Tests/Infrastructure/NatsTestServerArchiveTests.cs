using System.Net;
using System.Net.Sockets;

namespace Observables.Nats.Tests.Infrastructure;

public sealed class NatsTestServerArchiveTests
{
    [Fact]
    public void Truncated_zip_is_not_a_usable_archive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"obs-nats-poison-{Guid.NewGuid():N}.zip");
        File.WriteAllBytes(path, [0x50, 0x4B, 0x03, 0x04]);
        try
        {
            Assert.False(NatsTestServer.IsUsableArchive(path, zip: true));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Download_does_not_keep_a_partial_file_when_copy_is_canceled()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"obs-nats-dl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var archivePath = Path.Combine(directory, "nats-server.zip");
        using var cts = new CancellationTokenSource();
        await using var source = new CancellableStream(cts);
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => NatsTestServer.DownloadAtomicallyAsync(source, archivePath, cts.Token));
            Assert.False(File.Exists(archivePath));
            Assert.False(File.Exists(archivePath + ".partial"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Cross_process_binary_gate_can_await_before_release()
    {
        var observed = await NatsTestServer.WithCrossProcessBinaryGateAsync(
            async () =>
            {
                await Task.Yield();
                return 17;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(17, observed);
    }

    [Fact]
    public async Task WaitForPort_connects_after_earlier_attempts_failed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var wait = NatsTestServer.WaitForPortAsync(port, TestContext.Current.CancellationToken);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            await wait;
        }
        finally
        {
            listener.Stop();
        }
    }

    sealed class CancellableStream : Stream
    {
        readonly CancellationTokenSource cts;

        public CancellableStream(CancellationTokenSource cts) => this.cts = cts;

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

        public override int Read(byte[] buffer, int offset, int count)
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return 0;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cts.Cancel();
            await Task.Yield();
            throw new OperationCanceledException(cts.Token);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
