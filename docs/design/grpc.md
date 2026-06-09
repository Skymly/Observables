# gRPC Domain Design

## Overview

`Observables.Grpc` bridges `Grpc.Core.CallInvoker` (from `GrpcChannel.CreateCallInvoker()` or any gRPC client) to reactive streams.
Users declare a `[Grpc]` interface with boundary attributes; Roslyn source generators emit a proxy that maps RPC shapes to `Observable<T>` / `IObservable<T>`.

Integration wraps the existing gRPC stack — **no protoc / `Grpc.Tools` at consumer runtime**. Message types are typically `Google.Protobuf.IMessage<T>`; the runtime provides marshallers via `GrpcMarshallers`.

## Packages

| NuGet Package | Reactive Backend |
|---|---|
| `Observables.Grpc.R3` | R3 `Observable<T>` |
| `Observables.Grpc.Reactive` | System.Reactive `IObservable<T>` |

Both packages include the runtime (`Observables.Grpc`), the adapter layer, and the corresponding Roslyn source generator.

## Boundary Attributes

| Attribute | Applied To | gRPC Shape | Reactive Mapping |
|---|---|---|---|
| `[GrpcUnary(name?)]` | Method | Unary RPC | `Observable<TResp>` single value |
| `[GrpcServerStream(name?)]` | Method | Server streaming | `Observable<TResp>` multiple values |
| `[GrpcClientStream(name?)]` | Method | Client streaming | `Observable<TReq>` in → `Observable<TResp>` single value |
| `[GrpcDuplex(name?)]` | Method | Duplex streaming | `Observable<TReq>` in → `Observable<TResp>` stream |

`[Grpc(serviceName?)]` on the interface selects the gRPC service name (defaults to interface name without leading `I`).

## Member Shapes

### Unary

```csharp
[GrpcUnary("SayHello")]
Observable<EchoReply> SayHello(EchoRequest request, CancellationToken cancellationToken = default);
```

- One request parameter (plus optional trailing `CancellationToken`).
- Returns `Observable<TResponse>` (R3) or `IObservable<TResponse>` (Reactive).

### Server streaming

```csharp
[GrpcServerStream("StreamEcho")]
Observable<EchoReply> StreamEcho(EchoRequest request, CancellationToken cancellationToken = default);
```

- Same parameter shape as unary.
- Each `ResponseStream` item becomes `OnNext`; completes when the stream ends.

### Client streaming

```csharp
[GrpcClientStream("Collect")]
Observable<EchoReply> Collect(Observable<EchoRequest> requests, CancellationToken cancellationToken = default);
```

- First parameter is `Observable<TRequest>` (R3) or `IObservable<TRequest>` (Reactive).
- Request stream is completed when the input observable completes.
- Single response is emitted once `ResponseAsync` completes.

### Duplex streaming

```csharp
[GrpcDuplex("Chat")]
Observable<EchoReply> Chat(Observable<EchoRequest> requests, CancellationToken cancellationToken = default);
```

- First parameter is the outbound request stream.
- Each inbound `ResponseStream` item is emitted to the returned observable.

## CallInvoker Bridge

```
User code
  GrpcService.For<IMyService>(channel.CreateCallInvoker())
       ↓
Generated proxy (per interface)
  builds Method<TReq,TResp> + Marshaller<T> via GrpcMarshallers
       ↓
GrpcObservable / SystemReactiveGrpcAdapter
  AsyncUnaryCall / AsyncServerStreamingCall / AsyncClientStreamingCall / AsyncDuplexStreamingCall
       ↓
CallInvoker → remote gRPC service
```

`GrpcService` mirrors other domains: module initializers register `RegisterGeneratedFactory` entries at build time.

## Serialization (Marshaller) Boundary

- **Protobuf messages**: `GrpcMarshallers.ForMessage<T>()` where `T : IMessage<T>, new()`.
- **String payloads**: `GrpcMarshallers.String` (UTF-8) for simple scenarios and tests.
- Unsupported types fail at marshaller creation with `NotSupportedException`.

Generated proxies resolve marshallers from request/response type symbols at compile time.

## Design Decisions

### Why wrap `CallInvoker` instead of generating from `.proto`?

1. **Consistent declarative model** across Observables domains (RestAPI, SignalR, Mqtt, WebSocket).
2. **Reactive-first API** — users think in streams, not callback-style gRPC clients.
3. **No code-gen toolchain coupling** in the consumer project; proto/codegen remains optional on the server side.
4. **`Grpc.Core.Api`** supports `netstandard2.0`, matching the library TFM matrix.

### Why not embed `Grpc.Net.Client` in the runtime?

Consumers choose channel creation (`GrpcChannel.ForAddress`, DI, test hosts). The runtime only needs `CallInvoker`, keeping dependencies minimal.

## Diagnostic IDs (OBS7xxx)

| ID | Severity | Description |
|---|---|---|
| OBS7001 | Warning | Member has no gRPC boundary attribute |
| OBS7002 | Error | `Observables.Grpc` runtime not referenced |
| OBS7003 | Error | Unsupported return type |
| OBS7004 | Error | Member shape does not match boundary attribute |
| OBS7005 | Error | `IObservable<T>` without `Observables.Grpc.Reactive` |
| OBS7006 | Error | Unsupported parameter combination |
| OBS7007 | Error | Empty `[Grpc]` interface (`Observables.Analyzers`) |

## Entry Point

```csharp
var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = GrpcService.For<IMyService>(channel.CreateCallInvoker());
await client.SayHello(request).FirstAsync();
```
