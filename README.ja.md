# Observables

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | **日本語**

> **イベントおよび I/O 境界を R3 / System.Reactive へ橋渡しする Roslyn ソースジェネレーター群。** HTTP、SignalR、MQTT、WebSocket、gRPC、SSE、NATS、PostgreSQL LISTEN/NOTIFY、Redis Pub/Sub 向けの宣言的インターフェース駆動プロキシ——インターフェースを書くだけで `Observable<T>` が生成されます。

[![NuGet](https://img.shields.io/nuget/v/Observables.Events.R3.svg?label=NuGet&logo=nuget)](https://www.nuget.org/profiles/Skym)
[![CI](https://img.shields.io/github/actions/workflow/status/Skymly/Observables/ci.yml?branch=main&label=CI&logo=github)](https://github.com/Skymly/Observables/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Skymly/Observables.svg?label=License&logo=opensourcehardware)](LICENSE)

**リアクティブプログラミング（Rx）** 向けの Roslyn ソースジェネレータースイートです。宣言的なインターフェースを使って、.NET イベント、HTTP、SignalR、MQTT、WebSocket、gRPC、SSE、NATS、PostgreSQL LISTEN/NOTIFY、Redis Pub/Sub などの境界を **R3** または **System.Reactive** へ橋渡しします。

| リソース | リンク |
|----------|--------|
| ソース | [github.com/Skymly/Observables](https://github.com/Skymly/Observables) |
| ユーザードキュメント | [Observables.Docs](https://skymly.github.io/Observables.Docs/) |
| 実アプリ Showcase | [GitPulse](https://github.com/Skymly/GitPulse)（.NET MAUI · `Observables.RestAPI.R3` + `Observables.Events.R3`） |
| サンプルアプリ | [Observables.Samples](https://github.com/Skymly/Observables.Samples) |
| NuGet | [nuget.org/profiles/Skym](https://www.nuget.org/profiles/Skym) |
| コントリビュートとリリース | [CONTRIBUTING.md](./CONTRIBUTING.md) |
| セキュリティモデル | [SECURITY_MODEL.md](./SECURITY_MODEL.md) |

## ランタイムとパッケージ名

各機能ドメインはペアで公開され、**R3** と **System.Reactive** の依存関係は互いに混在しません。

| NuGet パッケージ ID | ランタイム |
|---------------------|------------|
| `Observables.<Feature>.R3` | [R3](https://github.com/Cysharp/R3) |
| `Observables.<Feature>.Reactive` | [System.Reactive](https://github.com/dotnet/reactive)（`IObservable<T>` 等） |

ソリューション内での開発およびテスト段階では、`Observables.Events.R3.SourceGenerators` などのプロジェクトを `ProjectReference` + `OutputItemType="Analyzer"` で参照します。

## クイックスタート

[NuGet](https://www.nuget.org/profiles/Skym) から対象ドメインのパッケージをインストールし（現在の安定版は [CONTRIBUTING.md](./CONTRIBUTING.md#releases-and-versioning) を参照）、リアクティブバックエンドパッケージを別途追加します。

```powershell
dotnet add package Observables.Events.R3
dotnet add package R3
```

System.Reactive 経路の場合は `Observables.Events.R3` を `Observables.Events.Reactive` に置き換え、`System.Reactive` パッケージを追加してください。

GitHub Packages からのインストールやコントリビュート・リリース手順は [CONTRIBUTING.md](./CONTRIBUTING.md) を参照してください。

## 機能ドメイン

10 個の機能ドメインすべてに両バックエンド向けのソースジェネレーターとテストがあり、必要に応じてランタイムも提供されます。共有層には `Observables.Core`、`Observables.Analyzers`、`Observables.CodeFixes` があります。

MQTT ドメインは MQTTnet 4/5 アダプターパッケージと中立 MQTT ランタイムを提供します。RestAPI、MQTT、Redis は .NET 8 以降でソース生成による JSON メタデータに対応しています。

| ドメイン | 説明 |
|----------|------|
| **Events** | 通常の .NET イベントおよびルーティングイベント → `Observable` / `IObservable` |
| **RestAPI** | 宣言的・型安全な HTTP クライアント |
| **SignalR** | Hub メンバープロキシ |
| **Mqtt** | MQTT トピックプロキシ |
| **WebSocket** | WebSocket クライアントプロキシ |
| **Grpc** | `CallInvoker` プロキシ |
| **Sse** | `text/event-stream` イベントストリーム |
| **Nats** | Core NATS サブジェクトプロキシ（v1 は JetStream 非対応） |
| **Postgres** | PostgreSQL LISTEN/NOTIFY チャネルプロキシ（専用の非プール接続；`Keepalive` 推奨） |
| **Redis** | Redis 標準 Pub/Sub チャネルプロキシ（完全一致 Channel + Pattern；`RedisMessage<T>` エンベロープ） |

設計ドキュメントは [`docs/`](docs/README.md)（ドキュメント体系は [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md)）を参照してください。ルーティングイベント生成は既定で無効です；利用側プロジェクトで `<ObservableRoutedEvents>true</ObservableRoutedEvents>` を設定してください（`Observables.Events/Observables.Events/targets/observables.events.props` を参照）。

### リポジトリ構成（概要）

| レイヤー | 説明 |
|----------|------|
| **`Observables.Core`** | ソリューション全体で共有されるランタイム |
| **`Observables.SourceGenerators.Shared`** | ジェネレーター共通基盤（link-compile 共有ソース） |
| **`Observables.<Feature>`** | ドメインランタイム（必要時のみ；Events のような純生成ドメインでは省略） |
| **`Observables.<Feature>.Reactive`** | System.Reactive 橋渡しランタイム（必要時のみ） |
| **`Observables.<Feature>.R3.SourceGenerators`** / **`.Reactive.SourceGenerators`** | 両バックエンド用ソースジェネレーター |
| **`Observables.<Feature>.Package`** | 上記 2 つの NuGet パッケージを生成するパック用プロジェクト |

## Events

通常の .NET イベント → `Observable<T>` / `IObservable<T>`。インターフェース定義は不要です——`event` メンバーを持つ任意の型に対して `.Events()` 拡張メソッドを呼び出すと、各イベントに対応する Observable プロパティが自動生成されます。

### 基本的な使い方

```csharp
// event を持つ任意のクラスまたはインターフェースで利用可能
public class Button
{
    public event EventHandler? Clicked;
    public event Action<string>? TextChanged;
}

// R3 経路
var btn = new Button();
using var d1 = btn.Events().Clicked.Subscribe(_ => Console.WriteLine("Clicked!"));
using var d2 = btn.Events().TextChanged.Subscribe(text => Console.WriteLine(text));

// System.Reactive 経路
using var d3 = btn.Events().Clicked.Subscribe(_ => Console.WriteLine("Clicked!"));
```

### Events vs EventHandlers

- **`.Events()`** — イベント引数を直接 `Observable<(T1, T2)>` タプルへ写像します（`sender` を省略）。ペイロードのみを扱う場面に適しています。
- **`.EventHandlers()`** — `(sender, EventArgs)` タプル形状を保持します。`sender` が必要な場面に適しています。

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

### ルーティングイベント（WPF / Avalonia）

既定では無効です。利用側プロジェクトで `<ObservableRoutedEvents>true</ObservableRoutedEvents>` を設定して有効化すると、ルーティングイベント向けの `.RoutedEvents()` / `.RoutedEventHandlers()` 拡張メソッドが生成されます。詳細は `Observables.Events/Observables.Events/targets/observables.events.props` を参照してください。

設計ドキュメントは `docs/design/events.md` を参照してください。

## RestAPI

宣言的で型安全な HTTP クライアントです。インターフェースメソッドに `[Get]` / `[Post]` / `[Put]` / `[Delete]` / `[Patch]` などの属性を付与すると、`HttpClient` プロキシ実装が生成されます。ルートテンプレート `{param}` と JSON シリアライズに対応しています。戻り値の型は選択したパッケージに依存します：`Observables.RestAPI.R3` は `Observable<T>` / `Task<T>`（R3 ジェネレーターは `IObservable<T>` を拒否）、`Observables.RestAPI.Reactive` は `IObservable<T>` / `Task<T>` を使用します。

### 基本的な使い方

```csharp
// インターフェース定義 — HttpClient 呼び出しの手書き不要
public interface IUserApi
{
    [Get("/users/{id}")]
    Observable<User> GetUser(int id);          // R3 経路

    [Post("/users")]
    Observable<User> CreateUser([Body] User user);

    [Delete("/users/{id}")]
    Observable<Unit> DeleteUser(int id);
}

// プロキシ生成
var api = RestService.For<IUserApi>("https://api.example.com");
using var d = api.GetUser(42).Subscribe(u => Console.WriteLine(u.Name));
```

### System.Reactive 経路

`Observable<T>` を `IObservable<T>` に置き換え、`Observables.RestAPI.Reactive` パッケージを参照します。

```csharp
public interface IUserApi
{
    [Get("/users/{id}")]
    IObservable<User> GetUser(int id);
}

var api = RestService.For<IUserApi>("https://api.example.com");
api.GetUser(42).Subscribe(u => Console.WriteLine(u.Name));
```

### NuGet インストール

リアクティブバックエンドに応じて NuGet パッケージを選択してください。どちらのパッケージにも `RestService` と `HttpClient` プロキシに必要なランタイムが含まれます。

| NuGet パッケージ ID | 同梱ランタイムアセンブリ | ソースジェネレーター |
|---------------------|--------------------------|----------------------|
| [`Observables.RestAPI.R3`](https://www.nuget.org/packages/Observables.RestAPI.R3) | `Observables.RestAPI.dll` | R3 |
| [`Observables.RestAPI.Reactive`](https://www.nuget.org/packages/Observables.RestAPI.Reactive) | `Observables.RestAPI.dll`, `Observables.RestAPI.Reactive.dll` | System.Reactive |

このドメインのランタイム部には [Refit](https://github.com/reactiveui/refit) から移植・適応されたコードが含まれます。ライセンス表記は [NOTICE.md](NOTICE.md) を参照してください。

## SignalR

`[Hub]` インターフェースのメンバーに `[HubInvoke]` / `[HubOn]` / `[HubSend]` / `[HubStream]` を付与すると、SignalR Hub プロキシが生成されます。`HubInvoke` はメソッド呼び出し、`HubOn` はサーバープッシュイベントに対応します。

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

[Microsoft.AspNetCore.SignalR.Client](https://www.nuget.org/packages/Microsoft.AspNetCore.SignalR.Client) に依存します。

## Mqtt

`[Mqtt]` インターフェースの購読プロパティに `[MqttSubscribe]`、公開メソッドに `[MqttPublish]` を付与します。クライアントのメジャーバージョンに合わせて `Observables.Mqtt.MQTTnet4` または `Observables.Mqtt.MQTTnet5` をインストールし、対応する名前空間をインポートしてください。

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

MQTTnet 5 アダプターパッケージは .NET 8 以降が必要です。クライアントの接続および破棄はアプリケーション側の責務です。[MQTT 設計・移行ガイド](docs/design/mqtt.md)

## WebSocket

`[WebSocket]` インターフェースの受信プロパティに `[WebSocketReceive]`、操作メソッドに `[WebSocketSend]` / `[WebSocketConnect]` / `[WebSocketClose]` を付与すると、WebSocket プロキシが生成されます。

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

`[Grpc]` インターフェースのメソッドに `[GrpcUnary]` / `[GrpcServerStream]` / `[GrpcClientStream]` / `[GrpcDuplex]` を付与すると、`CallInvoker` プロキシが生成されます。

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

どちらの gRPC バックエンドパッケージも `Grpc.Core.Api` に依存します。上の例ではアプリケーションから渡された `CallInvoker` を使用しており、トランスポートの選択はアプリケーション側に委ねられます。`GrpcChannel.ForAddress(...).CreateCallInvoker()` で invoker を作成する場合は [Grpc.Net.Client](https://www.nuget.org/packages/Grpc.Net.Client) をインストールしてください。

## Sse（Server-Sent Events）

`[Sse]` インターフェースのプロパティに `[SseEvent("イベント名")]` を付与すると、イベント名でフィルタリングし自動デシリアライズする `Observable<T>` / `IObservable<T>` プロキシが生成されます。引数なしの `[SseEvent]` は既定の `message` イベントに対応します。

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

`Subscribe` ごとに 1 本の SSE 接続を開始します；`string` はそのまま通過し、その他の型は `System.Text.Json` でデシリアライズされます。

## Nats（Core NATS）

`[Nats]` インターフェースのメンバーに `[NatsSubscribe]` / `[NatsPublish]` / `[NatsRequest]` を付与すると、購読用ホットストリーム、パブリッシュ用コールドストリーム、リクエスト-レスポンス単一値ストリームが生成されます。

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

[NATS.Client.Core](https://www.nuget.org/packages/NATS.Client.Core) に依存します。

## Postgres（LISTEN/NOTIFY）

`[Postgres]` インターフェースのメンバーに `[Listen]` / `[Notify]` を付与すると、LISTEN ホットストリームと NOTIFY コールドストリームが生成されます。`PostgresService.For<T>` は**専用の非プール化** `NpgsqlConnection` を受け取ります（長寿命の `Wait` 用に接続プールから接続を借りないでください）；Listen 用接続には `Pooling=false` と Npgsql の **`Keepalive`** 設定を推奨します。

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

[Npgsql](https://www.nuget.org/packages/Npgsql) に依存します。メンテナー向け設計ノートは [`docs/design/postgres.md`](docs/design/postgres.md) を参照してください。

## Redis（Pub/Sub）

`[Redis]` インターフェースのメンバーに `[RedisSubscribe]` / `[RedisPublish]` を付与すると、Subscribe ホットストリームと Publish コールドストリームが生成されます。`*` / `?` を含む Channel は Pattern 購読（`PSUBSCRIBE`）になり、`Observable<RedisMessage<T>>` を返す場合は具体的な Channel 名が付帯します。`RedisService.For<T>` は `IConnectionMultiplexer` を受け取ります。

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

[StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis) に依存します。メンテナー向け設計ノートは [`docs/design/redis.md`](docs/design/redis.md) を参照してください。v1 は Streams / keyspace / sharded Pub/Sub を**含みません**。

## ビルド

```powershell
cd Observables
dotnet build Observables.slnx

# 完全 CI（Nuke、GitHub Actions と同一）
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

完全な Nuke `Ci` の実行には **.NET 8、.NET 9、.NET 10 SDK** および対応するランタイム／テストホストをすべてインストールする必要があります。`global.json` は Nuke `build/` を **.NET 10 SDK** に固定しています（`rollForward` は同一メジャーバージョン内の `latestFeature` のみ）。ライブラリのターゲットは **netstandard2.0** / **net8.0** / **net9.0** / **net10.0** であり、ランタイム E2E テストは既定で **net8.0、net9.0、net10.0** 上で実行されるため、**.NET 9 テストホストは明示的な必須要件**です（8 と 10 だけでは不十分です）。メジャーを跨ぐ `DOTNET_ROLL_FORWARD` でネイティブ net9 テストを代替することは**できません**。これはコントリビューターのローカル環境契約であり、ライブラリのパッケージ不具合ではありません。ローカルでのパック、リリース手順、コントリビュート規約は [CONTRIBUTING.md](./CONTRIBUTING.md) を参照してください。

## ライセンス

Observables は MIT ライセンスのもとで公開されています — 詳細は [LICENSE](LICENSE) を参照してください。

`Observables.RestAPI` で利用している Refit 由来コードを含むサードパーティの著作権表示は [NOTICE.md](NOTICE.md) に記載されています。
