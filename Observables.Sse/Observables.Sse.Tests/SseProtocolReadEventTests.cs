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
}
