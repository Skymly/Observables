# WebSocket Domain Design

## Overview

`Observables.WebSocket` bridges `System.Net.WebSockets.ClientWebSocket` (BCL) to reactive streams.
The pattern mirrors the Mqtt domain: a source-generated proxy implements a user-defined interface
annotated with `[WebSocket]` and boundary attributes.

## Packages

| NuGet Package | Reactive Backend |
|---|---|
| `Observables.WebSocket.R3` | R3 `Observable<T>` |
| `Observables.WebSocket.Reactive` | System.Reactive `IObservable<T>` |

Both packages include the runtime (`Observables.WebSocket`), the adapter layer, and the
corresponding Roslyn source generator.

## Boundary Attributes

| Attribute | Applied To | Maps To |
|---|---|---|
| `[WebSocketConnect]` | Method | `ClientWebSocket.ConnectAsync` |
| `[WebSocketClose]` | Method | `ClientWebSocket.CloseAsync(NormalClosure)` |
| `[WebSocketSend]` | Method | `ClientWebSocket.SendAsync` |
| `[WebSocketReceive]` | Property | Background receive loop (`Observable<T>`) |

## Member Shapes

### Connect

```csharp
[WebSocketConnect]
Observable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);
```

- Exactly one `Uri` parameter (required).
- Optional trailing `CancellationToken`.
- Returns `Observable<Unit>` (R3) or `IObservable<Unit>` (Reactive).

### Close

```csharp
[WebSocketClose]
Observable<Unit> Close(CancellationToken cancellationToken = default);
```

- No non-CT parameters.
- Sends `WebSocketCloseStatus.NormalClosure`.

### Send

```csharp
[WebSocketSend("ping")]
Observable<Unit> Ping(CancellationToken cancellationToken = default);

[WebSocketSend]
Observable<Unit> SendMessage(string message);
```

- Parameter types supported: none (empty payload), `string` (UTF-8 text), `byte[]` (binary).
- For other types, payload is JSON-serialized (net8+ only).

### Receive

```csharp
[WebSocketReceive("message")]
Observable<string> Messages { get; }
```

- Read-only property (get-only).
- Cached (lazy `??=`): one subscription per proxy instance.
- Payload deserialized to `T`: `byte[]` (raw), `string` (UTF-8), or JSON (net8+).
- Completes when server sends a Close frame.

## Diagnostic IDs (OBS6xxx)

| ID | Severity | Description |
|---|---|---|
| OBS6001 | Warning | Member has no WebSocket boundary attribute |
| OBS6002 | Error | `Observables.WebSocket` not referenced |
| OBS6003 | Error | Unsupported return type |
| OBS6004 | Error | Member shape mismatch for boundary |
| OBS6005 | Error | System.Reactive not referenced for `IObservable` |
| OBS6006 | Error | Unsupported shape or parameter combination |

## Runtime Architecture

```
ClientWebSocket  ──►  WebSocketService.For<T>(socket)
                          │
                          ▼
              <T>GeneratedProxy (source-generated)
                    │           │
               Methods       Properties
          (cold streams)   (hot streams, cached)
                    │           │
           WebSocketObservable / SystemReactiveWebSocketAdapter
                          │
                    ClientWebSocket BCL APIs
```

## Design Decisions

- **No third-party dependency**: uses BCL `System.Net.WebSockets.ClientWebSocket` only.
- **Proxy takes a pre-constructed socket**: caller controls connection lifetime and configuration
  (headers, keep-alive, TLS, etc.) before passing to `WebSocketService.For<T>`.
- **Connect/Close as explicit boundary methods**: makes connection lifecycle visible in the
  interface contract and composable with reactive operators.
- **Receive uses lazy cached observable**: re-subscribing does not re-register the receive loop.
- **Send payload dispatch**: `string` → text frame (UTF-8), `byte[]` → binary frame, other types
  → JSON text frame (net8+ only; throws `NotSupportedException` on netstandard2.0).
