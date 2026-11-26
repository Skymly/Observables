# Observables

[English](README.md) | [简体中文](README.zh-CN.md) | **繁體中文** | [日本語](README.ja.md)

> **將事件與 IO 邊界橋接至 R3 / System.Reactive 的 Roslyn 原始碼產生器套件。** 為 HTTP、SignalR、MQTT、WebSocket、gRPC、SSE、NATS、PostgreSQL LISTEN/NOTIFY 與 Redis Pub/Sub 提供宣告式介面代理——寫出介面，即得 `Observable<T>`。

[![NuGet](https://img.shields.io/nuget/v/Observables.Events.R3.svg?label=NuGet&logo=nuget)](https://www.nuget.org/profiles/Skym)
[![CI](https://img.shields.io/github/actions/workflow/status/Skymly/Observables/ci.yml?branch=main&label=CI&logo=github)](https://github.com/Skymly/Observables/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Skymly/Observables.svg?label=License&logo=opensourcehardware)](LICENSE)

面向 **反應式程式設計（Rx）** 的 Roslyn 原始碼產生器套件：透過宣告式介面，將 .NET 事件、HTTP、SignalR、MQTT、WebSocket、gRPC、SSE、NATS、PostgreSQL LISTEN/NOTIFY、Redis Pub/Sub 等邊界橋接至 **R3** 或 **System.Reactive**。

| 資源 | 連結 |
|------|------|
| 原始碼 | [github.com/Skymly/Observables](https://github.com/Skymly/Observables) |
| 使用者文件 | [Observables.Docs](https://skymly.github.io/Observables.Docs/) |
| 實戰應用展示 | [GitPulse](https://github.com/Skymly/GitPulse)（.NET MAUI · `Observables.RestAPI.R3` + `Observables.Events.R3`） |
| 範例應用 | [Observables.Samples](https://github.com/Skymly/Observables.Samples) |
| NuGet | [nuget.org/profiles/Skym](https://www.nuget.org/profiles/Skym) |
| 貢獻與發布 | [CONTRIBUTING.md](./CONTRIBUTING.md) |
| 安全模型 | [SECURITY_MODEL.md](./SECURITY_MODEL.md) |

## 執行階段與套件名稱

每個功能域皆成對發布，**R3** 與 **System.Reactive** 互不混用相依性：

| NuGet 套件 ID | 執行階段 |
|---------------|----------|
| `Observables.<Feature>.R3` | [R3](https://github.com/Cysharp/R3) |
| `Observables.<Feature>.Reactive` | [System.Reactive](https://github.com/dotnet/reactive)（`IObservable<T>` 等） |

在解決方案內開發與測試階段，請透過 `ProjectReference` + `OutputItemType="Analyzer"` 直接參考對應的產生器專案（例如 `Observables.Events.R3.SourceGenerators`）。

## 快速開始

於 [NuGet](https://www.nuget.org/profiles/Skym) 安裝對應領域的套件（目前穩定版請參閱 [CONTRIBUTING.md](./CONTRIBUTING.md#releases-and-versioning)），並另外參考反應式後端套件：

```powershell
dotnet add package Observables.Events.R3
dotnet add package R3
```

若採用 System.Reactive 路徑，請將 `Observables.Events.R3` 改為 `Observables.Events.Reactive`，並新增 `System.Reactive` 套件。

從 GitHub Packages 安裝、參與貢獻或發布流程請參閱 [CONTRIBUTING.md](./CONTRIBUTING.md)。

## 功能域

十個功能域皆提供雙後端原始碼產生器與測試，並視需求提供執行階段。共用層另包含 `Observables.Core`、`Observables.Analyzers` 與 `Observables.CodeFixes`。

`0.5.1` 新增 MQTTnet 4/5 轉接套件與中立 MQTT 執行階段，合計共 **23 個套件**。RestAPI、MQTT 與 Redis 於 .NET 8 或更新版本支援原始碼產生的 JSON 中繼資料。

| 領域 | 說明 |
|------|------|
| **Events** | 傳統與路由 .NET 事件 → `Observable` / `IObservable` |
| **RestAPI** | 宣告式型別安全 HTTP 用戶端 |
| **SignalR** | Hub 成員代理 |
| **Mqtt** | MQTT 主題代理 |
| **WebSocket** | WebSocket 用戶端代理 |
| **Grpc** | `CallInvoker` 代理 |
| **Sse** | `text/event-stream` 事件串流 |
| **Nats** | Core NATS subject 代理（v1 不含 JetStream） |
| **Postgres** | PostgreSQL LISTEN/NOTIFY 通道代理（專用非集區連線；建議設定 keepalive） |
| **Redis** | Redis 傳統 Pub/Sub 通道代理（exact Channel + Pattern；`RedisMessage<T>` 信封） |

設計文件請參閱 [`docs/`](docs/README.md)（文件架構見 [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md)）。路由事件產生預設關閉；請在取用方專案設定 `<ObservableRoutedEvents>true</ObservableRoutedEvents>`（見 `Observables.Events/Observables.Events/targets/observables.events.props`）。

### 儲存庫結構（摘要）

| 層級 | 說明 |
|------|------|
| **`Observables.Core`** | 全套件共用執行階段 |
| **`Observables.SourceGenerators.Shared`** | 全套件共用產生器基礎設施（link-compile 共用原始碼） |
| **`Observables.<Feature>`** | 領域執行階段（視需求；純產生域如 Events 可省略） |
| **`Observables.<Feature>.Reactive`** | System.Reactive 橋接執行階段（視需求） |
| **`Observables.<Feature>.R3.SourceGenerators`** / **`.Reactive.SourceGenerators`** | 雙後端原始碼產生器 |
| **`Observables.<Feature>.Package`** | 封裝專案，產出上述兩個 NuGet 套件 |

## Events

傳統 .NET 事件 → `Observable<T>` / `IObservable<T>`。無需定義介面——對任何包含 `event` 成員的型別呼叫 `.Events()` 擴充方法，產生器即會自動為每個事件產出對應的 Observable 屬性。

### 基本用法

```csharp
// 任何含 event 的類別或介面皆可使用
public class Button
{
    public event EventHandler? Clicked;
    public event Action<string>? TextChanged;
}

// R3 路徑
var btn = new Button();
using var d1 = btn.Events().Clicked.Subscribe(_ => Console.WriteLine("Clicked!"));
using var d2 = btn.Events().TextChanged.Subscribe(text => Console.WriteLine(text));

// System.Reactive 路徑
using var d3 = btn.Events().Clicked.Subscribe(_ => Console.WriteLine("Clicked!"));
```

### Events vs EventHandlers

- **`.Events()`** — 將事件引數直接對應為 `Observable<(T1, T2)>` 元組（省略 `sender`），適合僅關注承載資料的情境。
- **`.EventHandlers()`** — 保留 `(sender, EventArgs)` 元組形狀，適合需要 `sender` 的情境。

```csharp
public interface INotifyPropertyChanged
{
    event PropertyChangedEventHandler? PropertyChanged;
}

// Events() → Observable<PropertyChangedEventArgs>
_ = obj.Events().PropertyChanged.Subscribe(e => Console.WriteLine(e.PropertyName));

// EventHandlers() → Observable<(object sender, PropertyChangedEventArgs e)>
_ = obj.EventHandlers().PropertyChanged.Subscribe(t => Console.WriteLine(t.e.PropertyName));
```

### 路由事件（WPF / Avalonia）

預設關閉。於取用方專案設定 `<ObservableRoutedEvents>true</ObservableRoutedEvents>` 啟用，產生器即會為路由事件產出 `.RoutedEvents()` / `.RoutedEventHandlers()` 擴充方法。詳見 `Observables.Events/Observables.Events/targets/observables.events.props`。

設計文件見 `docs/design/events.md`。

## RestAPI

宣告式型別安全 HTTP 用戶端。以 `[Get]` / `[Post]` / `[Put]` / `[Delete]` / `[Patch]` 等屬性標註介面方法，產生器自動產出 `HttpClient` 代理實作。支援路徑範本 `{param}` 與 JSON 序列化。傳回型別依所選套件而定：`Observables.RestAPI.R3` 使用 `Observable<T>` / `Task<T>`（R3 產生器會拒絕 `IObservable<T>`）；`Observables.RestAPI.Reactive` 使用 `IObservable<T>` / `Task<T>`。

### 基本用法

```csharp
// 定義介面 — 無需手寫 HttpClient 呼叫
public interface IUserApi
{
    [Get("/users/{id}")]
    Observable<User> GetUser(int id);          // R3 路徑

    [Post("/users")]
    Observable<User> CreateUser([Body] User user);

    [Delete("/users/{id}")]
    Observable<Unit> DeleteUser(int id);
}

// 建立代理
var api = RestService.For<IUserApi>("https://api.example.com");
using var d = api.GetUser(42).Subscribe(u => Console.WriteLine(u.Name));
```

### System.Reactive 路徑

將 `Observable<T>` 換為 `IObservable<T>`，並參考 `Observables.RestAPI.Reactive` 套件：

```csharp
public interface IUserApi
{
    [Get("/users/{id}")]
    IObservable<User> GetUser(int id);
}

var api = RestService.For<IUserApi>("https://api.example.com");
api.GetUser(42).Subscribe(u => Console.WriteLine(u.Name));
```

### NuGet 安裝

依反應式後端選擇一個 NuGet 套件。兩個套件皆包含 `RestService` 與 `HttpClient` 代理所需的執行階段。

| NuGet 套件 ID | 隨套件提供的執行階段組件 | 原始碼產生器 |
|---------------|--------------------------|--------------|
| [`Observables.RestAPI.R3`](https://www.nuget.org/packages/Observables.RestAPI.R3) | `Observables.RestAPI.dll` | R3 |
| [`Observables.RestAPI.Reactive`](https://www.nuget.org/packages/Observables.RestAPI.Reactive) | `Observables.RestAPI.dll`、`Observables.RestAPI.Reactive.dll` | System.Reactive |

此領域的執行階段部分包含改編自 [Refit](https://github.com/reactiveui/refit) 的程式碼，授權資訊請參閱 [NOTICE.md](NOTICE.md)。

## SignalR

於 `[Hub]` 介面上使用 `[HubInvoke]` / `[HubOn]` / `[HubSend]` / `[HubStream]` 標註成員，產生器即產出 SignalR Hub 代理。`HubInvoke` 對應方法呼叫，`HubOn` 對應伺服器推播事件。

```csharp
[Hub]
public interface IChatHub
{
    [HubInvoke]
    Observable<int> GetUserCount();

    [HubOn("ReceiveMessage")]
    Observable<ChatMessage> ReceiveMessage { get; }
}

var hub = HubService.For<IChatHub>(hubConnection);
using var d = hub.ReceiveMessage.Subscribe(msg => Console.WriteLine(msg.Text));
```

相依於 [Microsoft.AspNetCore.SignalR.Client](https://www.nuget.org/packages/Microsoft.AspNetCore.SignalR.Client)。

## Mqtt

於 `[Mqtt]` 介面上以 `[MqttSubscribe]` 標註訂閱屬性、以 `[MqttPublish]` 標註發布方法。自 0.5.1 起，依用戶端主版本安裝 `Observables.Mqtt.MQTTnet4` 或 `Observables.Mqtt.MQTTnet5`，並匯入對應命名空間。

```csharp
[Mqtt]
public interface ISensorHub
{
    [MqttSubscribe("sensors/temperature/#")]
    Observable<SensorData> Temperature { get; }

    [MqttPublish("commands/{deviceId}/reboot")]
    Observable<Unit> Reboot(string deviceId);
}

var hub = MqttService.For<ISensorHub>(MqttNetTransport.Wrap(mqttClient));
using var d = hub.Temperature.Subscribe(data => Console.WriteLine(data.Value));
```

MQTTnet 5 轉接套件需求為 .NET 8 或更新版本。應用程式負責連線與處置用戶端。[MQTT 設計與遷移說明](docs/design/mqtt.md)

## WebSocket

於 `[WebSocket]` 介面上以 `[WebSocketReceive]` 標註接收訊息屬性，以 `[WebSocketSend]` / `[WebSocketConnect]` / `[WebSocketClose]` 標註方法，產生器即產出 WebSocket 代理。

```csharp
[WebSocket]
public interface IRealtimeHub
{
    [WebSocketReceive("tick")]
    Observable<Tick> Ticks { get; }

    [WebSocketSend("subscribe")]
    Observable<Unit> Subscribe(string channel);
}

var hub = WebSocketService.For<IRealtimeHub>(clientWebSocket);
using var d = hub.Ticks.Subscribe(tick => Console.WriteLine(tick.Price));
```

## Grpc

於 `[Grpc]` 介面上以 `[GrpcUnary]` / `[GrpcServerStream]` / `[GrpcClientStream]` / `[GrpcDuplex]` 標註方法，產生器即產出 `CallInvoker` 代理。

```csharp
[Grpc("echo.Echo")]
public interface IEchoService
{
    [GrpcUnary("UnaryEcho")]
    Observable<string> UnaryEcho(string request);

    [GrpcServerStream("ServerStreamEcho")]
    Observable<string> ServerStreamEcho(string request);
}

var svc = GrpcService.For<IEchoService>(callInvoker);
using var d = svc.UnaryEcho("hello").Subscribe(reply => Console.WriteLine(reply));
```

兩個 gRPC 後端套件皆相依於 `Grpc.Core.Api`。上述範例使用應用程式傳入的 `CallInvoker`，傳輸層由應用程式決定。若透過 `GrpcChannel.ForAddress(...).CreateCallInvoker()` 建立 invoker，請安裝 [Grpc.Net.Client](https://www.nuget.org/packages/Grpc.Net.Client)。

## Sse（Server-Sent Events）

於 `[Sse]` 介面上以 `[SseEvent("名稱")]` 標註屬性，產生器即產出依事件名稱過濾、自動反序列化的 `Observable<T>` / `IObservable<T>` 代理。不帶引數的 `[SseEvent]` 會對應預設的 `message` 事件。

```csharp
[Sse]
public interface IPriceFeed
{
    [SseEvent("price")]
    Observable<PriceTick> Prices { get; }

    [SseEvent]
    Observable<string> Heartbeats { get; }
}

var feed = SseService.For<IPriceFeed>(new SseConnection(httpClient, endpoint));
using var d = feed.Prices.Subscribe(tick => Console.WriteLine(tick));
```

每次 `Subscribe` 都會建立一次 SSE 連線；`string` 直接透傳，其餘型別使用 `System.Text.Json` 反序列化。

## Nats（Core NATS）

於 `[Nats]` 介面上以 `[NatsSubscribe]` / `[NatsPublish]` / `[NatsRequest]` 標註成員，產生器即產出訂閱熱串流、發布冷串流與請求-回應單值串流。

```csharp
[Nats]
public interface IOrderHub
{
    [NatsSubscribe("orders.>")]
    Observable<OrderEvent> OrderEvents { get; }

    [NatsPublish("orders.{id}.cancel")]
    Observable<Unit> Cancel(string id);

    [NatsRequest("orders.validate")]
    Observable<ValidationResult> Validate(OrderRequest request);
}

await using var nats = new NatsConnection(new NatsOpts { Url = "nats://127.0.0.1:4222" });
var hub = NatsService.For<IOrderHub>(nats);
```

相依於 [NATS.Client.Core](https://www.nuget.org/packages/NATS.Client.Core)。

## Postgres（LISTEN/NOTIFY）

於 `[Postgres]` 介面上以 `[Listen]` / `[Notify]` 標註成員，產生器即產出 LISTEN 熱串流與 NOTIFY 冷串流。`PostgresService.For<T>` 接受**專用、非集區化**的 `NpgsqlConnection`（切勿從連線集區借用連線執行長生命週期的 `Wait`）；建議 LISTEN 連線設定 `Pooling=false` 並啟用 Npgsql **`Keepalive`**。

```csharp
[Postgres]
public interface IOrderHub
{
    [Listen("orders")]
    Observable<string> Orders { get; }

    [Notify("orders")]
    Observable<Unit> PublishOrder(string payload, CancellationToken cancellationToken = default);
}

await using var connection = new NpgsqlConnection(
    "Host=localhost;Database=app;Username=app;Password=…;Pooling=false;Keepalive=30");
await connection.OpenAsync();
var hub = PostgresService.For<IOrderHub>(connection);
```

相依於 [Npgsql](https://www.nuget.org/packages/Npgsql)。維護者設計說明見 [`docs/design/postgres.md`](docs/design/postgres.md)。

## Redis（Pub/Sub）

於 `[Redis]` 介面上以 `[RedisSubscribe]` / `[RedisPublish]` 標註成員，產生器即產出 Subscribe 熱串流與 Publish 冷串流。含 `*` / `?` 的 Channel 會走 Pattern（`PSUBSCRIBE`）；傳回 `Observable<RedisMessage<T>>` 時會附帶具體 Channel。`RedisService.For<T>` 接受 `IConnectionMultiplexer`。

```csharp
[Redis]
public interface INewsHub
{
    [RedisSubscribe("news.sports")]
    Observable<string> Sports { get; }

    [RedisSubscribe("news.*")]
    Observable<RedisMessage<string>> NewsFamily { get; }

    [RedisPublish("news.{topic}")]
    Observable<Unit> Publish(string topic, string payload, CancellationToken cancellationToken = default);
}

await using var mux = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var hub = RedisService.For<INewsHub>(mux);
```

相依於 [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis)。維護者設計說明見 [`docs/design/redis.md`](docs/design/redis.md)。v1 **不含** Streams / keyspace / sharded Pub/Sub。

## 建置

```powershell
cd Observables
dotnet build Observables.slnx

# 完整 CI（Nuke，與 GitHub Actions 一致）
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

完整 Nuke `Ci` 需要同時安裝 **.NET 8、.NET 9 與 .NET 10 SDK** 以及對應的執行階段 / testhost。`global.json` 將 Nuke `build/` 釘在 **.NET 10 SDK**（`rollForward` 僅限同一主要版本的 `latestFeature`）。程式庫目標框架為 **netstandard2.0** / **net8.0** / **net9.0** / **net10.0**；執行階段 E2E 預設於 **net8.0、net9.0、net10.0** 上執行，因此 **.NET 9 testhost 為明確需求**——僅安裝 8+10 並不足夠。跨主要版本的 `DOTNET_ROLL_FORWARD` **無法**替代原生 net9 測試。這是貢獻者的本機環境契約，並非程式庫安裝失敗。本機封裝、發布與貢獻規範請參閱 [CONTRIBUTING.md](./CONTRIBUTING.md)。

## 授權

Observables 採用 MIT 授權條款——詳見 [LICENSE](LICENSE)。

第三方授權聲明（包含 `Observables.RestAPI` 改編自 Refit 的程式碼）列於 [NOTICE.md](NOTICE.md)。
