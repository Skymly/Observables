# gRPC 域 — 开发设计文档

> 状态：**已实现**；NuGet `Observables.Grpc.R3` / `Observables.Grpc.Reactive` 已发 nuget.org。实现细节以代码为准。
> 命名、打包、诊断分段等约定以仓库根 [`AGENTS.md`](../../AGENTS.md) 为权威，本文在其框架内细化 gRPC 域。

## 1. 概述

`Observables.Grpc` 将 `Grpc.Core.CallInvoker`（来自 `GrpcChannel.CreateCallInvoker()` 或任意 gRPC 客户端）桥接到反应式流。
用户声明带边界特性的 `[Grpc]` 接口；Roslyn 源生成器生成代理，将 RPC 形态映射为 `Observable<T>` / `IObservable<T>`。

集成包装现有 gRPC 栈 —— **消费者运行时无需 protoc / `Grpc.Tools`**。消息类型通常为 `Google.Protobuf.IMessage<T>`；运行时通过 `GrpcMarshallers` 提供 marshaller。

## 2. 包

| NuGet 包 | 反应式后端 |
|---|---|
| `Observables.Grpc.R3` | R3 `Observable<T>` |
| `Observables.Grpc.Reactive` | System.Reactive `IObservable<T>` |

两个包均包含运行时（`Observables.Grpc`）、适配器层及对应的 Roslyn 源生成器。

## 3. 边界特性

| 特性 | 应用目标 | gRPC 形态 | 反应式映射 |
|---|---|---|---|
| `[GrpcUnary(name?)]` | 方法 | 一元 RPC | `Observable<TResp>` 单值 |
| `[GrpcServerStream(name?)]` | 方法 | 服务端流 | `Observable<TResp>` 多值 |
| `[GrpcClientStream(name?)]` | 方法 | 客户端流 | `Observable<TReq>` 输入 → `Observable<TResp>` 单值 |
| `[GrpcDuplex(name?)]` | 方法 | 双向流 | `Observable<TReq>` 输入 → `Observable<TResp>` 流 |

接口上的 `[Grpc(serviceName?)]` 选择 gRPC 服务名（默认为去掉前导 `I` 的接口名）。

## 4. 成员形态

### 4.1 一元

```csharp
[GrpcUnary("SayHello")]
Observable<EchoReply> SayHello(EchoRequest request, CancellationToken cancellationToken = default);
```

- 一个请求参数（外加可选的尾随 `CancellationToken`）。
- 返回 `Observable<TResponse>`（R3）或 `IObservable<TResponse>`（Reactive）。

### 4.2 服务端流

```csharp
[GrpcServerStream("StreamEcho")]
Observable<EchoReply> StreamEcho(EchoRequest request, CancellationToken cancellationToken = default);
```

- 参数形态与一元相同。
- 每个 `ResponseStream` 项触发 `OnNext`；流结束时完成。

### 4.3 客户端流

```csharp
[GrpcClientStream("Collect")]
Observable<EchoReply> Collect(Observable<EchoRequest> requests, CancellationToken cancellationToken = default);
```

- 第一个参数为 `Observable<TRequest>`（R3）或 `IObservable<TRequest>`（Reactive）。
- 输入 observable 完成时请求流完成。
- `ResponseAsync` 完成后发出单次响应。

### 4.4 双向流

```csharp
[GrpcDuplex("Chat")]
Observable<EchoReply> Chat(Observable<EchoRequest> requests, CancellationToken cancellationToken = default);
```

- 第一个参数为出站请求流。
- 每个入站 `ResponseStream` 项发往返回的 observable。

## 5. CallInvoker 桥接

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

`GrpcService` 与其他域一致：模块初始化器在构建期注册 `RegisterGeneratedFactory` 条目。

## 6. 序列化（Marshaller）边界

- **Protobuf 消息**：`GrpcMarshallers.ForMessage<T>()`，其中 `T : IMessage<T>, new()`。
- **字符串载荷**：`GrpcMarshallers.String`（UTF-8），用于简单场景与测试。
- 不支持的类型在 marshaller 创建时以 `NotSupportedException` 失败。

生成的代理在编译期从请求/响应类型符号解析 marshaller。

## 7. 设计决策

### 7.1 为何包装 `CallInvoker` 而非从 `.proto` 生成？

1. **一致的声明式模型**，跨 Observables 各域（RestAPI、SignalR、Mqtt、WebSocket）。
2. **反应式优先 API** —— 用户以流思考，而非回调式 gRPC 客户端。
3. **消费者项目无代码生成工具链耦合**；proto/codegen 在服务端仍为可选。
4. **`Grpc.Core.Api`** 支持 `netstandard2.0`，与库 TFM 矩阵匹配。

### 7.2 为何不在运行时内嵌 `Grpc.Net.Client`？

消费者自行选择 channel 创建方式（`GrpcChannel.ForAddress`、DI、测试宿主）。运行时仅需 `CallInvoker`，保持依赖最小化。

## 8. 诊断 ID（OBS7xxx）

| ID | Severity | 描述 |
|---|---|---|
| OBS7001 | Warning | 成员无 gRPC 边界特性 |
| OBS7002 | Error | 未引用 `Observables.Grpc` 运行时 |
| OBS7003 | Error | 不支持的返回类型 |
| OBS7004 | Error | 成员形态与边界特性不匹配 |
| OBS7005 | Error | `IObservable<T>` 但未引用 `Observables.Grpc.Reactive` |
| OBS7006 | Error | 不支持的参数组合 |
| OBS7007 | Warning | 空 `[Grpc]` 接口（`Observables.Analyzers`） |

## 9. 入口

```csharp
var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = GrpcService.For<IMyService>(channel.CreateCallInvoker());
await client.SayHello(request).FirstAsync();
```
