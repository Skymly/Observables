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

三个运行时程序集按后端分层（[ADR-003](../adr/ADR-003-backend-neutral-domain-runtime.md)）：

| 程序集 | 依赖 | 进哪个包 |
|--------|------|----------|
| `Observables.WebSocket`（`WebSocketProtocol`、`WebSocketReceivePump`、`WebSocketService`） | 无后端依赖 | 两个包都进 |
| `Observables.WebSocket.R3`（`WebSocketObservable`） | 上面那个 + R3 | 仅 `Observables.WebSocket.R3` |
| `Observables.WebSocket.Reactive`（`SystemReactiveWebSocketAdapter`） | 上面第一个 + System.Reactive | 仅 `Observables.WebSocket.Reactive` |

命名空间都是 `Observables.WebSocket`，拆分对消费者源码不可见。接收泵是 internal 且后端中立，留在第一个程序集里，两个桥接项目通过 `InternalsVisibleTo` 共用同一张 per-socket 表——这正是 §7 那条「每 socket 一个泵」得以跨后端成立的前提。

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
- 写了 `messageName` 时（上例的 `"ping"`），名字是它唯一能去的地方——WebSocket 没有 subject / topic 可绑，只有报文本身。此时该方法改发 JSON 信封文本帧 `{"type":"<messageName>","payload":<参数>}`，无参数时省略 `payload`，多参数时 `payload` 为参数组成的对象。信封走 JSON，因此和其他 JSON 发送一样仅 net8+，netstandard2.0 上抛 `NotSupportedException`；`byte[]` 参数在信封里会变成 base64 字符串而不再是二进制帧。没写名字的发送不受影响。

### 4.4 Receive

```csharp
[WebSocketReceive("message")]
Observable<string> Messages { get; }
```

- 只读属性（仅 get）。
- 缓存（惰性 `??=`）：每个代理实例一个序列。
- 载荷反序列化为 `T`：`byte[]`（原始）、`string`（UTF-8）或 JSON（net8+）。
- 当服务器发送 Close 帧时完成。
- 重组后的单条消息默认上限 **1 MiB**（`WebSocketObservable.DefaultMaxReceiveMessageBytes`）。`FromReceive(socket, maxMessageBytes)` 可覆盖。超出则接收流以错误完成，避免无界缓冲。
- 每个 `ClientWebSocket` 只有一个接收循环（`WebSocketReceivePump`），多个 `[WebSocketReceive]` 成员、多个订阅者共用它，各自反序列化自己的 `T`。上限由启动该循环的那次订阅决定，后来者沿用。
- 写了 `messageName` 时（上例的 `"message"`），该成员改走 `FromReceiveNamed`：只认 §4.3 具名发送写出的那个 JSON 信封，`type` 对得上才把 `payload` 反序列化为 `T` 发出去。名字对不上、或者压根不是信封的帧，直接跳过而不是报错——同一个 socket 上别的成员还要用它们。信封走 JSON，因此和具名发送一样仅 net8+，netstandard2.0 上抛 `NotSupportedException`；`payload` 恒按 JSON 解，所以 `string` 也走序列化器、`byte[]` 收的是 base64，正好和发送端对称。没写名字的接收拿到的仍是原始帧。
- 具名发送不带参数时信封里没有 `payload`，此时具名接收无从构造 `T`，抛 `InvalidOperationException`。要观察这类纯信号，用不具名接收看原始帧。

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
- **每 socket 一个接收泵**：`ClientWebSocket` 不允许并发接收，一个订阅一条循环会让 BCL 把每条消息只交给其中一个挂起的 `ReceiveAsync`，其余订阅者永远收不到。所以循环按 socket 唯一（`ConditionalWeakTable` 索引），首次订阅时启动，之后把消息扇出给所有 sink。
- **最后一个订阅者离开时不停泵**：取消挂起的 `ReceiveAsync` 会连带 abort 整个 socket，发送也一起废掉。没有订阅者的泵读完即丢——WebSocket 客户端本来也得持续接收才能处理控制帧。泵在 socket 关闭或出错时结束。
- **Send 载荷分派**：`string` → 文本帧（UTF-8），`byte[]` → 二进制帧，其他类型
  → JSON 文本帧（仅 net8+；在 netstandard2.0 上抛出 `NotSupportedException`）。
- **具名 Send 用 `{"type","payload"}` 信封**：名字在生成代码里直接成为匿名对象的字面量，运行时不读特性，
  免得给 trim / AOT 分析器添告警。信封形状是本域自己定的约定——服务端协议千差万别，选一个写进文档，
  比让 `messageName` 继续当摆设强。
- **具名 Receive 读同一个信封**：`[WebSocketReceive(messageName)]` 认的就是具名 Send 写的那个 `type`，两边共用一套约定。
  过滤放在成员层而不是泵里：泵仍然只管收原始帧，每个具名成员自己试着拆信封。否则为了给一个成员分流，
  整条链路上的 `byte[]` / `string` 成员都得先被当成 JSON 试一遍。名字对不上就跳过而不是报错，正因为同一个
  socket 上的其他成员还指望这些帧。
