using System.Net;
using System.Net.Sockets;

namespace Observables.Nats.Tests.Infrastructure;

public sealed class NatsTestServerArchiveTests
{
    const string ExpectedSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Cache_lookup_checks_the_entire_file_against_the_pinned_digest()
    {
        var path = Path.Combine(Path.GetTempPath(), $"obs-nats-cache-{Guid.NewGuid():N}");
        try
        {
            Assert.False(NatsTestServer.HasSha256(path, ExpectedSha256));
            File.WriteAllBytes(path, "abc"u8.ToArray());
            Assert.True(NatsTestServer.HasSha256(path, ExpectedSha256));
            File.WriteAllBytes(path, "abd"u8.ToArray());
            Assert.False(NatsTestServer.HasSha256(path, ExpectedSha256));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Copy_with_wrong_digest_keeps_the_verified_file_and_cleans_up_the_partial_file()
    {
        var directory = Directory.CreateTempSubdirectory("obs-nats-digest-");
        var path = Path.Combine(directory.FullName, "artifact");
        var cancellation = TestContext.Current.CancellationToken;
        try
        {
            using var correct = new MemoryStream("abc"u8.ToArray());
            await NatsTestServer.WriteVerifiedFileAsync(correct, path, ExpectedSha256, cancellation);
            using var wrong = new MemoryStream("abd"u8.ToArray());
            await Assert.ThrowsAsync<InvalidDataException>(
                () => NatsTestServer.WriteVerifiedFileAsync(wrong, path, ExpectedSha256, cancellation));

            Assert.True(NatsTestServer.HasSha256(path, ExpectedSha256));
            Assert.Single(directory.GetFiles());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Verified_copy_does_not_keep_a_partial_file_when_canceled()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"obs-nats-dl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var archivePath = Path.Combine(directory, "nats-server.zip");
        using var cts = new CancellationTokenSource();
        await using var source = new CancellableStream(cts);
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => NatsTestServer.WriteVerifiedFileAsync(source, archivePath, ExpectedSha256, cts.Token));
            Assert.Empty(Directory.GetFiles(directory));
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
