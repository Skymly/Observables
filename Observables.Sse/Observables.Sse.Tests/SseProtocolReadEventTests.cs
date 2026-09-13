using System.Text;

namespace Observables.Sse.Tests;

public sealed class SseProtocolReadEventTests
{
    [Fact]
    public async Task ReadEventAsync_skips_empty_data_and_returns_the_next_event()
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
    public async Task ReadEventAsync_does_not_dispatch_an_empty_data_field()
    {
        const string stream =
            "event: price\n" +
            "data:\n" +
            "\n";

        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stream)));

        var sseEvent = await SseProtocol.ReadEventAsync(reader);

        Assert.False(sseEvent.HasValue);
    }
}
