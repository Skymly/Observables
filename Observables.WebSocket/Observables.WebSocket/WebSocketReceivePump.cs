using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

namespace Observables.WebSocket;

// Receives what a WebSocketReceivePump reads off the socket.
internal interface IWebSocketReceiveSink
{
    void OnMessage(byte[] payload, WebSocketMessageType messageType);

    // The server closed the connection.
    void OnClosed();

    // The pump stopped because of this error.
    void OnFailed(Exception error);

    // The pump hit this error and kept reading.
    void OnTransientError(Exception error);
}

// Runs one reassembly loop per ClientWebSocket and fans the messages out to every sink.
//
// ClientWebSocket forbids concurrent receives, so a loop per subscription races: the BCL hands each message to
// exactly one of the pending ReceiveAsync calls and the other subscribers never see it.
//
// The loop starts on the first subscription and then runs until the socket closes or fails. It is deliberately
// not stopped when the last sink detaches: cancelling a pending ReceiveAsync aborts the whole socket, which
// would take sends down with it. A pump with no sinks reads and discards, which is what a WebSocket client has
// to do anyway to keep control frames flowing.
internal sealed class WebSocketReceivePump
{
    static readonly ConditionalWeakTable<ClientWebSocket, WebSocketReceivePump> Pumps = new();

    readonly ClientWebSocket socket;
    readonly object gate = new();
    readonly List<IWebSocketReceiveSink> sinks = new();
    bool running;

    WebSocketReceivePump(ClientWebSocket socket) => this.socket = socket;

    internal static WebSocketReceivePump For(ClientWebSocket socket)
    {
        if (socket is null)
        {
            throw new ArgumentNullException(nameof(socket));
        }

#if NETSTANDARD2_0
        return Pumps.GetValue(socket, s => new WebSocketReceivePump(s));
#else
        return Pumps.GetValue(socket, static s => new WebSocketReceivePump(s));
#endif
    }

    // Attaches the sink and starts the loop if it is not already running. When it starts, it takes
    // maxMessageBytes as its cap; a sink that attaches later inherits that cap.
    internal IDisposable Subscribe(IWebSocketReceiveSink sink, int maxMessageBytes)
    {
        var start = false;
        lock (gate)
        {
            sinks.Add(sink);
            if (!running)
            {
                running = true;
                start = true;
            }
        }

        if (start)
        {
            _ = Task.Run(() => RunAsync(maxMessageBytes));
        }

        return new Attachment(this, sink);
    }

    void Detach(IWebSocketReceiveSink sink)
    {
        lock (gate)
        {
            sinks.Remove(sink);
        }
    }

    async Task RunAsync(int maxMessageBytes)
    {
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceivedMessage? message;
                try
                {
                    message = await WebSocketProtocol
                        .ReceiveMessageAsync(socket, CancellationToken.None, maxMessageBytes)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (WebSocketProtocol.MessageTooLargeException ex)
                {
                    Dispatch(sink => sink.OnFailed(ex));
                    return;
                }
                catch (Exception ex)
                {
                    Dispatch(sink => sink.OnTransientError(ex));
                    continue;
                }

                if (message is null)
                {
                    Dispatch(static sink => sink.OnClosed());
                    return;
                }

                var received = message.Value;
                Dispatch(sink => sink.OnMessage(received.Payload, received.MessageType));
            }
        }
        finally
        {
            lock (gate)
            {
                running = false;
            }
        }
    }

    void Dispatch(Action<IWebSocketReceiveSink> notification)
    {
        IWebSocketReceiveSink[] snapshot;
        lock (gate)
        {
            if (sinks.Count == 0)
            {
                return;
            }

            snapshot = sinks.ToArray();
        }

        foreach (var sink in snapshot)
        {
            notification(sink);
        }
    }

    sealed class Attachment : IDisposable
    {
        readonly WebSocketReceivePump pump;
        readonly IWebSocketReceiveSink sink;
        int disposed;

        internal Attachment(WebSocketReceivePump pump, IWebSocketReceiveSink sink)
        {
            this.pump = pump;
            this.sink = sink;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                pump.Detach(sink);
            }
        }
    }
}
