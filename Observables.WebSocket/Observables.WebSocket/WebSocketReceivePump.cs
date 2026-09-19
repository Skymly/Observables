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
// The loop starts on the first subscription. If the socket is not Open yet, the pump waits while any sink is
// attached (subscribe-then-Connect). It keeps running until the socket closes or fails even if sinks detach:
// cancelling a pending ReceiveAsync aborts the whole socket.
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
    // Non-positive caps fail this sink once and do not start the loop.
    internal IDisposable Subscribe(IWebSocketReceiveSink sink, int maxMessageBytes)
    {
        if (maxMessageBytes <= 0)
        {
            sink.OnFailed(new ArgumentOutOfRangeException(nameof(maxMessageBytes)));
            return EmptyDisposable.Instance;
        }

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

    bool HasSinks()
    {
        lock (gate)
        {
            return sinks.Count > 0;
        }
    }

    async Task RunAsync(int maxMessageBytes)
    {
        try
        {
            if (!await WaitUntilOpenAsync().ConfigureAwait(false))
            {
                DispatchTerminalForCurrentState();
                return;
            }

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceivedMessage? message;
                try
                {
                    message = await WebSocketProtocol
                        .ReceiveMessageAsync(socket, CancellationToken.None, maxMessageBytes)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    if (IsTerminal(socket.State))
                    {
                        Dispatch(sink => sink.OnFailed(ex));
                    }

                    return;
                }
                catch (WebSocketProtocol.MessageTooLargeException ex)
                {
                    Dispatch(sink => sink.OnFailed(ex));
                    return;
                }
                catch (Exception ex) when (IsUnrecoverable(ex))
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

            DispatchTerminalForCurrentState();
        }
        finally
        {
            lock (gate)
            {
                running = false;
            }
        }
    }

    async Task<bool> WaitUntilOpenAsync()
    {
        while (true)
        {
            var state = socket.State;
            if (state == WebSocketState.Open)
            {
                return true;
            }

            if (IsTerminal(state) || !HasSinks())
            {
                return false;
            }

            await Task.Delay(15).ConfigureAwait(false);
        }
    }

    void DispatchTerminalForCurrentState()
    {
        if (!HasSinks())
        {
            return;
        }

        if (socket.State == WebSocketState.Aborted)
        {
            Dispatch(static sink => sink.OnFailed(
                new WebSocketException("The WebSocket was aborted.")));
            return;
        }

        if (IsTerminal(socket.State))
        {
            Dispatch(static sink => sink.OnClosed());
        }
    }

    bool IsUnrecoverable(Exception exception)
    {
        if (exception is WebSocketException or ObjectDisposedException)
        {
            return true;
        }

        return IsTerminal(socket.State);
    }

    static bool IsTerminal(WebSocketState state) =>
        state is WebSocketState.Aborted
            or WebSocketState.Closed
            or WebSocketState.CloseReceived
            or WebSocketState.CloseSent;

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
            try
            {
                notification(sink);
            }
            catch (Exception)
            {
                // One sink must not prevent the others from seeing the same event.
            }
        }
    }

    sealed class EmptyDisposable : IDisposable
    {
        internal static readonly EmptyDisposable Instance = new();

        public void Dispose()
        {
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
