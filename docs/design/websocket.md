# WebSocket 域 — 开发设计文档

> 状态：**已实现**；NuGet `Observables.WebSocket.R3` / `Observables.WebSocket.Reactive` 已发 nuget.org。实现细节以代码为准。
> 命名、打包、诊断分段等约定以仓库根 [`AGENTS.md`](../../AGENTS.md) 为权威，本文在其框架内细化 WebSocket 域。

## 1. 概述

`Observables.WebSocket` 将 `System.Net.WebSockets.ClientWebSocket`（BCL）桥接为反应式流。
该模式与 Mqtt 域一致：源生成器代理实现一个由 `[WebSocket]` 与边界特性标注的用户定义接口。

## 2. 包

| NuGet 包 | 反应式后端 |
|---|---|
| `Observables.WebSocket.R3` | R3 `Observable<T>` |
| `Observables.WebSocket.Reactive` | System.Reactive `IObservable<T>` |

两个包均包含运行时（`Observables.WebSocket`）、适配层以及对应的 Roslyn 源生成器。

## 3. 边界特性

| 特性 | 应用目标 | 映射至 |
|---|---|---|
| `[WebSocketConnect]` | 方法 | `ClientWebSocket.ConnectAsync` |
| `[WebSocketClose]` | 方法 | `ClientWebSocket.CloseAsync(NormalClosure)` |
| `[WebSocketSend]` | 方法 | `ClientWebSocket.SendAsync` |
| `[WebSocketReceive]` | 属性 | 后台接收循环（`Observable<T>`） |

## 4. 成员形状

### 4.1 Connect

```csharp
[WebSocketConnect]
Observable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);
```

- 恰好一个 `Uri` 参数（必需）。
- 可选的末尾 `CancellationToken`。
- 返回 `Observable<Unit>`（R3）或 `IObservable<Unit>`（Reactive）。

### 4.2 Close

```csharp
[WebSocketClose]
Observable<Unit> Close(CancellationToken cancellationToken = default);
```

- 无非 CT 参数。
- 发送 `WebSocketCloseStatus.NormalClosure`。

### 4.3 Send

```csharp
[WebSocketSend("ping")]
Observable<Unit> Ping(CancellationToken cancellationToken = default);

[WebSocketSend]
Observable<Unit> SendMessage(string message);
```

- 支持的参数类型：无（空载荷）、`string`（UTF-8 文本）、`byte[]`（二进制）。
- 对于其他类型，载荷以 JSON 序列化（仅 net8+）。

### 4.4 Receive

```csharp
[WebSocketReceive("message")]
Observable<string> Messages { get; }
```

- 只读属性（仅 get）。
- 缓存（惰性 `??=`）：每个代理实例一次订阅。
- 载荷反序列化为 `T`：`byte[]`（原始）、`string`（UTF-8）或 JSON（net8+）。
- 当服务器发送 Close 帧时完成。

## 5. 诊断 ID（OBS6xxx）

| ID | Severity | 说明 |
|---|---|---|
| OBS6001 | Warning | 成员无 WebSocket 边界特性 |
| OBS6002 | Error | 未引用 `Observables.WebSocket` |
| OBS6003 | Error | 不支持的返回类型 |
| OBS6004 | Error | 成员形状与边界不匹配 |
| OBS6005 | Error | 使用 `IObservable` 但未引用 System.Reactive |
| OBS6006 | Error | 不支持的形状或参数组合 |

## 6. 运行时架构

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

## 7. 设计决策

- **无第三方依赖**：仅使用 BCL `System.Net.WebSockets.ClientWebSocket`。
- **代理接收预构造的 socket**：调用方在传递给 `WebSocketService.For<T>` 之前控制连接生命周期与配置
  （头、保活、TLS 等）。
- **Connect/Close 作为显式边界方法**：使连接生命周期在接口契约中可见，并可与反应式运算符组合。
- **Receive 使用惰性缓存 observable**：重新订阅不会重新注册接收循环。
- **Send 载荷分派**：`string` → 文本帧（UTF-8），`byte[]` → 二进制帧，其他类型
  → JSON 文本帧（仅 net8+；在 netstandard2.0 上抛出 `NotSupportedException`）。
