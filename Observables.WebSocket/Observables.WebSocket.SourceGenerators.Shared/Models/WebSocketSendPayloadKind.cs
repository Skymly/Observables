namespace Observables.WebSocket.Generators;

internal enum WebSocketSendPayloadKind : byte
{
    None,
    Text,
    Binary,
    Json,
}
