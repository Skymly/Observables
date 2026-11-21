using System.Text;

namespace Observables.Sse.Tests;

public sealed class SseProtocolReadEventTests
{
    [Fact]
    public async Task ReadEventAsync_skips_id_only_block_and_returns_the_next_event()
    {
        const string stream =
            "id: 1\n" +
            "\n" +
            "event: price\n" +
            "data: 100\n" +
            "\n";

        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stream)));

        var sseEvent = await SseProtocol.ReadEventAsync(reader);

        Assert.True(sseEvent.HasValue);
        Assert.Equal("price", sseEvent.Value.EventName);
        Assert.Equal("100", sseEvent.Value.Data);
        Assert.Null(sseEvent.Value.Id);
    }

    [Fact]
    public async Task ReadEventAsync_dispatches_an_empty_data_field()
    {
        const string stream =
            "event: price\n" +
            "data:\n" +
            "\n";

        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stream)));

        var sseEvent = await SseProtocol.ReadEventAsync(reader);

        Assert.True(sseEvent.HasValue);
        Assert.Equal("price", sseEvent.Value.EventName);
        Assert.Equal(string.Empty, sseEvent.Value.Data);
    }

    [Fact]
    public async Task ReadEventAsync_drops_a_half_event_at_eof()
    {
        const string stream =
            "event: price\n" +
            "data: 100\n";

        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stream)));

        var sseEvent = await SseProtocol.ReadEventAsync(reader);

        Assert.False(sseEvent.HasValue);
    }

    [Fact]
    public async Task ReadEventAsync_whitespace_event_name_is_message()
    {
        const string stream =
            "event:   \n" +
            "data: beat\n" +
            "\n";

        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stream)));

        var sseEvent = await SseProtocol.ReadEventAsync(reader);

        Assert.True(sseEvent.HasValue);
        Assert.Equal("message", sseEvent.Value.EventName);
        Assert.Equal("beat", sseEvent.Value.Data);
    }

    [Fact]
    public async Task ReadEventAsync_overlong_line_fails()
    {
        var stream = "data: " + new string('x', 32) + "\n\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stream)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SseProtocol.ReadEventAsync(reader, maxLineBytes: 8, maxEventBytes: 1024, CancellationToken.None));
        Assert.Contains("line", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("\n", "A", 7)]
    [InlineData("\r", "A", 7)]
    [InlineData("\r\n", "A", 7)]
    [InlineData("\n", "\U0001F680", 10)]
    [InlineData("\r", "\U0001F680", 10)]
    [InlineData("\r\n", "\U0001F680", 10)]
    public async Task ReadEventAsync_counts_UTF8_payload_bytes_without_the_line_ending(
        string lineEnding, string data, int bytes)
    {
        string stream = $"data: {data}{lineEnding}{lineEnding}";
        using var reader = CreateChunkedReader(stream);
        var token = TestContext.Current.CancellationToken;

        var result = await SseProtocol.ReadEventAsync(reader, bytes, bytes + 1, token);

        Assert.Equal(data, Assert.NotNull(result).Data);

        using var tooLong = CreateChunkedReader(stream);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SseProtocol.ReadEventAsync(tooLong, bytes - 1, bytes + 1, token));
        Assert.Contains("SSE line", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadEventAsync_does_not_treat_a_split_CRLF_as_two_terminators()
    {
        using var reader = CreateChunkedReader("data: first\r\ndata: second\r\n\r\ndata: next\r\n\r\n");

        Assert.Equal("first\nsecond", Assert.NotNull(await SseProtocol.ReadEventAsync(reader)).Data);
        Assert.Equal("next", Assert.NotNull(await SseProtocol.ReadEventAsync(reader)).Data);
        Assert.Null(await SseProtocol.ReadEventAsync(reader));
    }

    static StreamReader CreateChunkedReader(string stream) =>
        new(new OneByteAtATimeStream(Encoding.UTF8.GetBytes(stream)));

    sealed class OneByteAtATimeStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
