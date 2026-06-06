namespace Observables.WebSocket.Generators;

internal enum WebSocketBoundaryKind : byte
{
    Send,
    Receive,
    Connect,
    Close,
}
