# 调研：尚未覆盖的事件 / IO 边界候选

> 取数日期：**2026-07-27**。所有 NuGet 版本、发布日期与许可证均取自 nuget.org registration API
> （`https://api.nuget.org/v3/registration5-semver1/<id>/index.json` 的 `catalogEntry.version` /
> `published` / `licenseExpression`），其余论断均标注一手来源（官方文档、库源码或规范）。
>
> 本文是**调研**，不是路线图承诺。已覆盖的 8 域（Events、RestAPI、SignalR、Mqtt、WebSocket、Grpc、Sse、Nats）不在候选内。

## 1. 判定口径

### 1.1 形态判定

现有 7 个 IO 代理域的形态是：**用户写打了 `[Domain]` 的 interface，成员上打方法级特性，生成器 emit 代理类 + `ModuleInitializer` 工厂注册，入口 `XxxService.For<T>(...)`**（见 [`docs/design/architecture.md`](../design/architecture.md) §4、[`docs/design/nats.md`](../design/nats.md) §2）。判定分三档：

| 档 | 含义 |
|----|------|
| **契合** | 边界语义可完全用「特性字面量参数 + 返回 `Observable<T>` 的属性/方法」表达；订阅即热流、发布即冷流，无需用户提供额外状态 |
| **部分** | 主干可以表达，但有**必须暴露给用户**的额外语义（ack/offset/checkpoint/schema/租约），会撑大 API 面 |
| **不契合** | 订阅不是「声明一个位点」，而是要求用户提供状态机、租约存储、位点提交或服务端 endpoint，接口代理只能变成薄壳 |

### 1.2 E2E 可测性分级

以仓库现状为基准：8 个域的 E2E 全部不依赖外部服务与凭据；其中 **NATS 的「进程内」实际是「测试首次运行时从 GitHub Release 下载 `nats-server` 便携二进制并在本机起进程」**（[`Observables.Nats.Tests/Infrastructure/NatsTestServer.cs`](../../Observables.Nats/Observables.Nats.Tests/Infrastructure/NatsTestServer.cs)）。因此分级为：

| 级 | 含义 | 仓库先例 |
|----|------|----------|
| **A** | **纯进程内**：服务端/broker 是 NuGet 库，直接在测试进程里 new 出来 | Mqtt（进程内 MQTTnet broker）、Grpc（`TestServer`）、Sse/WebSocket（内嵌 HTTP server） |
| **B** | **下载二进制 + 本机子进程**，无 Docker、无凭据 | Nats（下载 `nats-server`） |
| **C** | 需 **Docker / JVM / 云 CLI**（CI 需新增运行时依赖） | 无先例 |
| **D** | 需**真实硬件、驱动、外部服务或凭据** | 无先例 |

CI 当前是 Windows + Ubuntu 双矩阵（ROADMAP E9），因此**任何 Windows-only 或 Linux-only 的边界都会打破矩阵对称性**，这是一个独立于 A–D 的减分项。

## 2. 总览表

共 **35 个候选**。★ 标记为「形态契合 + E2E ≤ B 级 + 无凭据」的第一梯队。

| # | 候选边界 | 典型 .NET 客户端库（最新稳定版 / 发布日 / 许可证） | 形态 | E2E | 需外部凭据 |
|---|----------|--------------------------------------------------|------|-----|-----------|
| 1 | Apache Kafka | `Confluent.Kafka` 2.15.0 / 2026-07-01 / Apache-2.0 | 部分 | C | 否（需 Docker/JVM） |
| 2 | RabbitMQ（AMQP 0-9-1） | `RabbitMQ.Client` 7.2.1 / 2026-02-26 / Apache-2.0 OR MPL-2.0 | 部分 | C | 否（需 Docker） |
| 3 | ★ AMQP 1.0 | `AMQPNetLite` 2.5.3 / 2026-06-03 / Apache-2.0 | 契合 | **A** | 否 |
| 4 | Azure Service Bus | `Azure.Messaging.ServiceBus` 7.20.2 / 2026-07-11 / MIT | 部分 | C | 否（emulator 为 Docker + EULA） |
| 5 | Azure Event Hubs | `Azure.Messaging.EventHubs` 5.12.2 / 2025-06-13 / MIT | 部分 | C | 否（emulator 为 Docker） |
| 6 | AWS SQS / SNS | `AWSSDK.SQS` / `AWSSDK.SimpleNotificationService` 4.0.100.6 / 2026-07-23 / Apache-2.0 | 部分 | C | 否（LocalStack）/ 是（真云） |
| 7 | Google Cloud Pub/Sub | `Google.Cloud.PubSub.V1` 3.36.0 / 2026-06-16 / Apache-2.0 | 部分 | C | 否（emulator 需 gcloud + JDK） |
| 8 | ★ Redis Pub/Sub | `StackExchange.Redis` 3.0.17 / 2026-07-11 / MIT | **契合** | **A**（Garnet） | 否 |
| 9 | Redis Streams | 同上 | 不契合 | A | 否 |
| 10 | ★ ZeroMQ | `NetMQ` 4.0.4.2 / 2026-05-24 / **LGPLv3** | 契合 | **A** | 否 |
| 11 | Apache Pulsar | `DotPulsar` 5.3.1 / 2026-05-04 / Apache-2.0 | 部分 | C | 否（需 Docker/JVM） |
| 12 | EventStoreDB / KurrentDB | `KurrentDB.Client` 1.4.0 / 2026-05-07 / 包内 license 文件 | 部分 | C | 否（需 Docker） |
| 13 | ★ PostgreSQL LISTEN/NOTIFY | `Npgsql` 10.0.3 / 2026-05-27 / PostgreSQL | 契合 | **B** | 否 |
| 14 | PostgreSQL 逻辑复制 | `Npgsql` 同上 | 不契合 | B | 否 |
| 15 | SQL Server SqlDependency | `Microsoft.Data.SqlClient` 7.0.2 / 2026-06-26 / MIT | 部分 | C | 否（需 SQL Server 实例） |
| 16 | ★ MongoDB Change Streams | `MongoDB.Driver` 3.10.0 / 2026-07-08 / Apache-2.0 | 部分 | **B**（EphemeralMongo） | 否 |
| 17 | Cosmos DB Change Feed | `Microsoft.Azure.Cosmos` 3.62.0 / 2026-07-17 / MIT | **不契合** | C | 否（emulator） |
| 18 | ★ GraphQL Subscriptions | `StrawberryShake.Transport.WebSockets` / `HotChocolate.AspNetCore` 16.5.1 / 2026-07-22 / MIT | 契合 | **A** | 否 |
| 19 | 入站 Webhook | ASP.NET Core（BCL） | 契合（方向反转） | **A** | 否 |
| 20 | ★ FileSystemWatcher | BCL `System.IO` | 契合 | **A** | 否 |
| 21 | 串口 SerialPort | `System.IO.Ports` 10.0.10 / 2026-07-15 / MIT | 契合（缺分帧） | **D** | 否（需虚拟串口驱动） |
| 22 | MIDI | `Melanchall.DryWetMidi` 8.0.3 / 2025-12-16 / MIT；`NAudio.Midi` 2.3.0 / 2026-03-13 / MIT | 契合 | **D** | 否（需虚拟 MIDI 设备） |
| 23 | WMI 事件 | `System.Management` 10.0.10 / 2026-07-15 / MIT | **契合** | A（**Windows-only**） | 部分事件需管理员 |
| 24 | Windows 事件日志 | `System.Diagnostics.EventLog` 10.0.10 / 2026-07-15 / MIT | 契合 | B（**Windows-only**，写日志源需管理员） | 否 |
| 25 | ETW / EventPipe | `Microsoft.Diagnostics.Tracing.TraceEvent` 3.2.5 / 2026-07-18 / MIT；`Microsoft.Diagnostics.NETCore.Client` 0.2.661903 / 2026-01-07 / MIT | 部分 | C/D（ETW 需管理员） | 否 |
| 26 | ★ .NET 诊断源（EventSource / DiagnosticListener / Activity / Meter） | BCL `System.Diagnostics.DiagnosticSource` | 契合 | **A** | 否 |
| 27 | 蓝牙 BLE / HID | `InTheHand.BluetoothLE` 4.0.44 / 2025-12-06 / MIT；`HidSharp` 2.6.4 / 2025-10-14 / 包内 license | 契合 | **D**（需硬件） | 否 |
| 28 | ★ 命名管道 / Unix domain socket | BCL `System.IO.Pipes` / `System.Net.Sockets` | 契合（缺分帧） | **A** | 否 |
| 29 | D-Bus | `Tmds.DBus` 0.94.2 / 2026-06-17 / MIT | 契合（**与官方 codegen 重叠**） | B（**Linux-only**） | 否 |
| 30 | Orleans Streams | `Microsoft.Orleans.Server` 10.2.2 / 2026-07-22 / MIT；`Microsoft.Orleans.TestingHost` 9.2.1 / 2025-07-17 / MIT | 部分 | **A** | 否 |
| 31 | Akka.NET EventStream | `Akka` 1.5.70 / 2026-07-03 / Apache-2.0 | 部分（价值重叠） | **A** | 否 |
| 32 | Dapr pub/sub | `Dapr.Client` / `Dapr.AspNetCore` 1.18.5 / 2026-07-25 / Apache-2.0 | **不契合** | C | 否（需 sidecar + 组件） |
| 33 | ★ OPC UA | `OPCFoundation.NetStandard.Opc.Ua` 1.5.378.156 / 2026-07-10 / OPC Foundation MIT License | **契合** | A/B | 否 |
| 34 | CoAP Observe | `CoAPnet` 1.2.0 / **2022-04-30** / MIT | 契合 | **D**（无进程内 server） | 否 |
| 35 | IMAP IDLE | `MailKit` 4.17.0 / 2026-05-27 / MIT | 部分 | **D** | 是（无进程内 IMAP server） |

---

## 3. 消息队列与流

### 3.1 Apache Kafka

- **客户端库**：`Confluent.Kafka` 2.15.0（2026-07-01，Apache-2.0）。是 librdkafka 的托管封装，依赖 `librdkafka.redist`（[README「Referencing」](https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/README.md)）。
- **形态：部分**。消费面是**同步拉取循环** `c.Consume(cts.Token)`，官方基础示例即 `while(true) { var cr = c.Consume(ct); }`；订阅通过 `c.Subscribe("my-topic")`，偏移量默认自动提交但生产用法要手动提交，且 `c.Close()` 才能「干净地离开消费组并提交最终偏移」（同上 README「Basic Consumer Example」注释）。
  - 卡点一：**消费组 rebalance** 会重分区，订阅生命周期不等于 `Observable` 生命周期。
  - 卡点二：**offset 提交必须暴露**，否则语义退化为 at-most-once。
  - 可行的最小面（放弃手动提交）：

    ```csharp
    [Kafka]
    public interface IOrderStream
    {
        [KafkaConsume("orders", GroupId = "svc")] Observable<OrderEvent> Orders { get; }
        [KafkaProduce("orders")] Observable<Unit> Publish(OrderEvent e);
    }
    ```

    但这等于把 Kafka 降级成 pub/sub，对 Kafka 用户是**反模式**。
- **E2E：C 级**。无进程内 .NET broker。Apache Kafka 自身是 JVM 应用——官方 README 明确「You need to have Java installed」（[apache/kafka README](https://github.com/apache/kafka/blob/trunk/README.md)）。替代是 `Testcontainers.Kafka` 4.13.0（2026-07-03，MIT），需要 Docker daemon。
- **凭据**：本地测试不需要；Confluent Cloud 才需要。

### 3.2 RabbitMQ（AMQP 0-9-1）

- **客户端库**：`RabbitMQ.Client` 7.2.1（2026-02-26，Apache-2.0 OR MPL-2.0）。
- **形态：部分（主干很顺）**。官方 .NET API 指南把 `IAsyncBasicConsumer` 称为「推送 API」，并给出便捷类 `AsyncEventingBasicConsumer`，其 `ReceivedAsync` 是 C# 事件；订阅返回 consumer tag，用 `IChannel.BasicCancelAsync` 取消（[.NET API Guide「Retrieving Messages By Subscription」](https://www.rabbitmq.com/client-libraries/dotnet-api-guide)）。这与我们把「事件 → 热流属性、取消 → dispose」的映射完全同构。
  - 卡点：**手动 ack**。指南示例在 handler 里 `await channel.BasicAckAsync(ea.DeliveryTag, false)`；且自动恢复场景下「使用手动 ack 的应用必须能处理重复投递」（同上「Automatic Recovery」段）。要么强制 `autoAck: true`（丢语义），要么让流元素携带 ack 句柄：

    ```csharp
    [Rabbit]
    public interface IOrderQueue
    {
        [RabbitConsume("orders", AutoAck = false)]
        Observable<RabbitDelivery<OrderEvent>> Orders { get; }   // 元素上带 .AckAsync()
    }
    ```

- **E2E：C 级**。RabbitMQ broker 是 Erlang 服务，无 .NET 进程内实现；`Testcontainers.RabbitMq` 4.13.0（2026-07-03，MIT）需要 Docker。

### 3.3 ★ AMQP 1.0（AMQP.Net Lite）

- **客户端库**：`AMQPNetLite` 2.5.3（2026-06-03，Apache-2.0）。
- **形态：契合**。链路模型 `ReceiverLink` / `SenderLink` 与「订阅属性 / 发布方法」一一对应。
- **E2E：A 级（本组最强的意外收获）**。README 首段即写明「The library includes both a client and **listener** to enable peer to peer and broker based messaging」，并把「Listener APIs to enable wide range of listener applications, **including brokers**」列为特性（[Azure/amqpnetlite README](https://github.com/Azure/amqpnetlite/blob/master/README.md)）。也就是说，**可以在测试进程内用同一个库起一个最小 AMQP 1.0 broker**，无需 Docker、无需二进制下载。注意 README 的平台表格标注 Mono/Linux 上「只验证了 client API，listener API 状态未知」——Ubuntu CI 需要实测。
- **凭据**：否。
- **附带价值**：AMQP 1.0 是 Azure Service Bus / Event Hubs / ActiveMQ Artemis 的底层协议，一个 AMQP 1.0 域可以间接覆盖它们的一部分（但不覆盖各家的管理面与 settlement 语义）。

### 3.4 Azure Service Bus / 3.5 Azure Event Hubs

- **客户端库**：`Azure.Messaging.ServiceBus` 7.20.2（2026-07-11，MIT）；`Azure.Messaging.EventHubs` 5.12.2（2025-06-13，MIT）。
- **形态：部分**。Service Bus 的 `ServiceBusProcessor` 是事件式（`ProcessMessageAsync` / `ProcessErrorAsync`），可映射成两条流；但**消息结算**（Complete / Abandon / DeadLetter / Defer）、锁续约、会话（session）都必须暴露，接口特性表达不了。Event Hubs 的 `EventProcessorClient` 还要求一个 **checkpoint store**（通常是 Blob 容器），属于「用户必须提供状态存储」。
- **E2E：C 级**。Service Bus 官方模拟器「runs as a Docker container (Linux based)」，且「available under the Microsoft Software License Terms」，重启后数据与实体不持久（[Service Bus emulator overview](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator)）。Event Hubs 也有对应的本地模拟器（[Event Hubs emulator overview](https://learn.microsoft.com/en-us/azure/event-hubs/overview-emulator)），同为容器化本地工具。
- **凭据**：本地模拟器不需要云账号；真实命名空间需要连接字符串 / Entra ID。

### 3.6 AWS SQS / SNS

- **客户端库**：`AWSSDK.SQS` 与 `AWSSDK.SimpleNotificationService` 均为 4.0.100.6（2026-07-23，Apache-2.0）。
- **形态：部分 / 不契合**。SQS 没有服务端推送，只有**长轮询 `ReceiveMessage` + 显式 `DeleteMessage`**（可见性超时是核心语义）；SNS 更是**只有发布端**，订阅端是 HTTP endpoint / SQS 队列 / Lambda，客户端库里根本没有「订阅并接收」的 API。把 SQS 包成 `Observable<T>` 等于把轮询循环藏起来，会误导用户。
- **E2E：C 级**：`Testcontainers.LocalStack` 4.13.0（2026-07-03，MIT），需 Docker。
- **凭据**：LocalStack 不需要真凭据；真 AWS 需要。

### 3.7 Google Cloud Pub/Sub

- **客户端库**：`Google.Cloud.PubSub.V1` 3.36.0（2026-06-16，Apache-2.0）。
- **形态：部分**。`SubscriberClient.StartAsync(handler)` 的 handler 必须返回 `Reply.Ack` / `Reply.Nack`——ack 是控制流的一部分（[Pub/Sub emulator 文档中的 C# 示例](https://docs.cloud.google.com/pubsub/docs/emulator)）。
- **E2E：C 级**。官方模拟器通过 gcloud CLI 安装运行（`gcloud components install pubsub-emulator`），前置条件明确要求「Install a JDK」；且 **C# 与 Java 客户端不会自动读 `PUBSUB_EMULATOR_HOST`，必须改代码设置 `EmulatorDetection`**（同上）。
- **凭据**：模拟器不需要；真服务需要 ADC。

### 3.8 ★ Redis Pub/Sub

- **客户端库**：`StackExchange.Redis` 3.0.17（2026-07-11，MIT）。
- **形态：契合（本表最干净的之一）**。`multiplexer.GetSubscriber().Subscribe("messages", (channel, message) => …)` 是纯推送，**没有 ack、没有游标、没有消费组**；库还原生提供顺序 vs 并发两种分发模式（`channel.OnMessage(...)` 顺序 / `Subscribe(channel, handler)` 并发，见 [Pub/Sub Message Order](https://stackexchange.github.io/StackExchange.Redis/PubSubOrder.html)）——这正好对应 R3 的调度选项。
  ```csharp
  [Redis]
  public interface INotifications
  {
      [RedisSubscribe("news.*")] Observable<NewsItem> News { get; }      // 支持 glob 模式，同 Nats 通配符
      [RedisPublish("news.{topic}")] Observable<Unit> Publish(string topic, NewsItem item);
  }
  ```
- **E2E：A 级（纯进程内，无需 Docker，无需下载二进制）**。**Microsoft Garnet** 是用 C# 写的 RESP 协议服务端，以 `Microsoft.Garnet` 2.1.0（2026-07-24，MIT）发布为 NuGet **库**；其 `GarnetServer` 是 public 类型，暴露 `public GarnetServer(string[] commandLineArgs, …)` 与 `public void Start()`，内部持有 `SubscribeBroker`（[libs/host/GarnetServer.cs](https://github.com/microsoft/garnet/blob/main/libs/host/GarnetServer.cs)）。Garnet README 明确「adopts the popular RESP wire protocol … makes it possible to use Garnet from unmodified Redis clients … such as StackExchange.Redis in C#」（[microsoft/garnet README](https://github.com/microsoft/garnet/blob/main/README.md)）。因此测试形态可以做到和 Mqtt 域的进程内 MQTTnet broker 完全一样。
- **待验证**：Garnet 对 pub/sub 命令族（`SUBSCRIBE` / `PSUBSCRIBE` / `PUBLISH`）的具体覆盖与行为差异，须对照 Garnet 的 RESP 命令兼容性文档实测一次再定案。

### 3.9 Redis Streams

- **形态：不契合**。`StackExchange.Redis` 的 Stream API 全部是**非阻塞、需显式位点**的：`StreamRead("events_stream", "0-0")`、`StreamReadGroup(key, group, consumer, ">")`、以及必须调用的 `StreamAcknowledge`（[Streams 文档](https://stackexchange.github.io/StackExchange.Redis/Streams)）。没有 blocking read，要做成流只能由我们代生成一个**轮询循环**并替用户决定退避策略与位点存储——这是把复杂度藏起来而不是消除它。
- **结论**：即便 Redis 域上马，也应只做 Pub/Sub，Streams 明确列为非目标（可参照 Nats 域把 JetStream 列为 follow-up 的先例，见 [`docs/design/nats.md`](../design/nats.md) §9）。

### 3.10 ★ ZeroMQ（NetMQ）

- **客户端库**：`NetMQ` 4.0.4.2（2026-05-24）。**许可证是 LGPLv3**——README 首句即「This is an open source project licensed under the LGPLv3」，NuGet 的 licenseUrl 指向 `COPYING.LESSER`（[zeromq/netmq README](https://github.com/zeromq/netmq/blob/master/README.md)）。这是全表唯一的 copyleft 依赖，若 `Observables.Zmq` 域运行时直接引用它，包的依赖链就带上 LGPL，需要先做许可证决策。
- **形态：契合，但需要选子集**。PUB/SUB 天然是流；但 ZeroMQ 还有 REQ/REP、DEALER/ROUTER、PUSH/PULL、PAIR 等 socket 类型，语义差别很大，域设计必须先划定支持哪几种。
- **E2E：A 级**。ZeroMQ 是 **brokerless** 的，`inproc://` 与 `tcp://127.0.0.1` 传输在同一测试进程内即可对接，不需要任何外部服务端。

### 3.11 Apache Pulsar（DotPulsar）

- **客户端库**：`DotPulsar` 5.3.1（2026-05-04，Apache-2.0），Apache 官方 .NET 客户端。
- **形态：部分**。消费面 `await foreach (var message in consumer.Messages())` 是 `IAsyncEnumerable`，映射成 `Observable<T>` 很自然；但同一段官方示例紧接着就是 `await consumer.Acknowledge(message)`，且 consumer 构建时**必须提供 `Schema`**（`client.NewConsumer(Schema.String)`）与 `SubscriptionName`（[apache/pulsar-dotpulsar README](https://github.com/apache/pulsar-dotpulsar/blob/master/README.md)）。Schema 是构造期的强类型参数，可以由返回类型推导；ack 与订阅类型（exclusive / shared / failover / key-shared）则必须暴露。
- **E2E：C 级**。README 直接说「we need a Pulsar setup. See Pulsar docs for how to set up a local **standalone** Pulsar instance」——Pulsar standalone 是 JVM 进程；`Testcontainers.Pulsar` 4.13.0（2026-07-03，MIT）需 Docker。

### 3.12 EventStoreDB / KurrentDB

- **客户端库**：`KurrentDB.Client` 1.4.0（2026-05-07）；旧名 `EventStore.Client.Grpc.Streams` 23.3.9（2025-05-14）。两者 NuGet 均以**包内 license 文件**声明（非 SPDX 表达式），采用前需人工核对授权条款。
- **形态：部分**。`SubscribeToStream` / `SubscribeToAll` 本身是持久推送流，映射极自然；但持久订阅要求**位点/checkpoint 提交**与 `FromStream.After(position)` 恢复语义，属于「必须暴露游标」。
- **E2E：C 级**（服务端为独立二进制 / Docker 镜像）。

---

## 4. 数据库变更流

### 4.1 ★ PostgreSQL LISTEN / NOTIFY

- **客户端库**：`Npgsql` 10.0.3（2026-05-27，PostgreSQL 许可证）。
- **形态：契合**。Npgsql 把通知暴露为 `NpgsqlConnection.Notification` 事件，并提供 `Wait()` / 带超时版本 / 异步版本用于「在同步请求-响应周期之外接收通知」（[Npgsql「Waiting for Notifications」](https://www.npgsql.org/doc/wait.html)）。
  ```csharp
  [PgListen]
  public interface IOrderChannel
  {
      [Listen("order_created")] Observable<string> OrderCreated { get; }          // payload 是字符串，可再 JSON 反序列化
      [Notify("order_created")] Observable<Unit> Raise(string payload);
  }
  ```
- **唯一真正的卡点**：同一文档指出「Npgsql 只在常规（同步）查询交互中处理通知消息」，要在请求周期外收到通知就必须持续 `Wait`。这意味着**一条连接被订阅独占，且不能来自连接池**——「连接生命周期 = 订阅生命周期」这条约束必须写进域设计，`For<T>()` 的入参应该是一个专用连接或连接工厂，而不是 `NpgsqlDataSource`。文档同时建议开启 keepalive。
- **E2E：B 级**。PostgreSQL 是独立二进制，但可以照搬 NATS 的做法下载便携版；NuGet 上有 `MysticMind.PostgresEmbed`、`PostgreSql.Binaries.Lite` 等第三方封装（nuget.org 搜索「embedded postgres」，2026-07-27），维护状态需逐个核实；也可用 `Testcontainers.PostgreSql`（需 Docker，降为 C 级）。
- **凭据**：否。

### 4.2 PostgreSQL 逻辑复制

- **形态：不契合**。Npgsql 提供 `LogicalReplicationConnection`，但用户必须自己管理 **replication slot 的创建/删除**、按 LSN 调用 `SetReplicationStatus` 确认、选择输出插件（pgoutput / wal2json）并解析其消息类型。这是「用户提供状态机 + 显式位点提交」的典型，接口特性只能表达 slot 名。
- **结论**：不建议。

### 4.3 SQL Server SqlDependency / 查询通知

- **客户端库**：`Microsoft.Data.SqlClient` 7.0.2（2026-06-26，MIT）。第三方封装 `SqlTableDependency` 最后发布于 **2020-01-21**，已停滞。
- **形态：部分，且有一个致命语义**。`SqlDependency` 绑定到一个 `SqlCommand`，通过 `OnChange` 事件通知结果集发生变化（[Detecting Changes with SqlDependency](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/detecting-changes-with-sqldependency)）。但它是**一次性**的：触发后依赖失效，必须重新执行命令重新注册——包成 `Observable<T>` 后用户会误以为是持续流。此外通知**不带变更数据**，只说「结果变了」。
- **前置条件很重**（[Enabling Query Notifications](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/enabling-query-notifications)）：数据库须 `ALTER DATABASE … SET ENABLE_BROKER`（Service Broker）、须创建 queue 与 service、用户须有 `SUBSCRIBE QUERY NOTIFICATIONS` 权限、SELECT 语句须满足「Creating a Query for Notification」的一长串限制、且应用启动/退出须成对调用 `SqlDependency.Start` / `Stop`。
- **E2E：C 级**。需要真实 SQL Server 实例（Windows 上 LocalDB / Express，Linux 上仅 Docker 镜像），双 OS 矩阵下没有对称方案。

### 4.4 ★ MongoDB Change Streams

- **客户端库**：`MongoDB.Driver` 3.10.0（2026-07-08，Apache-2.0）。
- **形态：部分（偏契合）**。C# 用法是 `inventory.Watch()` 返回游标，官方示例即 `while (cursor.MoveNext() …)`（[MongoDB Manual「Change Streams」](https://www.mongodb.com/docs/manual/changestreams/)）。映射成 `Observable<ChangeStreamDocument<T>>` 很自然：
  ```csharp
  [MongoWatch]
  public interface IInventoryChanges
  {
      [Watch("shop.inventory", Operations = ChangeOps.Insert | ChangeOps.Update)]
      Observable<ChangeStreamDocument<InventoryItem>> Items { get; }
  }
  ```
  卡点有二：(a) **resume token** —— 断线续传要求保存并回放 token，若不暴露就只能「从现在开始」；(b) 过滤是**聚合管道**，用特性字面量只能表达最常见的 `operationType` 子集。
- **E2E：B 级**。变更流**要求副本集**：官方文档明确「Change streams are available for replica sets and sharded clusters」，且各语言示例都以「connected to a MongoDB replica set」为前提（同上）。`EphemeralMongo` 3.2.0（2025-07-07，Apache-2.0）正好满足：README 声明「Support for **single-node replica sets, enabling transactions and change streams**」，并说明它在运行时下载官方 `mongod` 二进制、校验 SHA256、随机端口启动、Dispose 时清理，支持 Linux/macOS/Windows（[asimmon/ephemeral-mongo README](https://github.com/asimmon/ephemeral-mongo/blob/main/README.md)）。这与 NATS 域现有的「下载二进制 + 起子进程」模式完全同级。
- **凭据**：否。

### 4.5 Cosmos DB Change Feed

- **客户端库**：`Microsoft.Azure.Cosmos` 3.62.0（2026-07-17，MIT）。
- **形态：不契合**。Change Feed Processor 的四个组成部分是 monitored container、**lease container**、compute instance（须有唯一 `instanceName`）、delegate；构建方式是 `GetChangeFeedProcessorBuilder<T>(processorName, onChangesDelegate).WithInstanceName(...).WithLeaseContainer(...)`，delegate 接收的是 `IReadOnlyCollection<T>` **批次**，checkpoint 由处理器写入 lease 文档；文档甚至警告「delegate 里做异步处理可能在完成前就 checkpoint，导致漏事件」（[Change feed processor](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/change-feed-processor)）。也就是说：用户必须提供**另一个容器**作为状态存储、必须给部署单元命名、语义是批处理而非逐条流。这不是「打个特性就订阅」的边界。
- **E2E：C 级**。模拟器为 Docker 容器或 Windows 本地安装，且 Linux 模拟器不支持 Apple silicon / ARM（[Cosmos DB emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/emulator)）。
- **结论**：明确不建议。

---

## 5. Web 与 RPC

### 5.1 ★ GraphQL Subscriptions

- **客户端 / 服务端库**：`StrawberryShake.Transport.WebSockets` 与 `HotChocolate.AspNetCore` 均为 16.5.1（2026-07-22，MIT）；协议为 `graphql-transport-ws`（[graphql-ws PROTOCOL.md](https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md)，其中「The WebSocket sub-protocol for this specification is: `graphql-transport-ws`」）。
- **形态：契合**。订阅在客户端就是「发一段 query 文本，收一串 `Next` 消息」，可直接映射：
  ```csharp
  [GraphQL("https://api.example.com/graphql")]
  public interface IOrderApi
  {
      [Subscription("subscription { onOrder { id total } }")]
      Observable<OrderPayload> OnOrder { get; }

      [Query("query($id:ID!){ order(id:$id){ id total } }")]
      Observable<OrderPayload> GetOrder(string id);
  }
  ```
- **两个真实风险**：(a) **没有 schema 就没有编译期校验**——我们只能校验「返回类型能否反序列化」，查询文本对不对要到运行时才知道；而引入 schema 就意味着要读 `.graphql` 文件（`AdditionalFiles`），这是现有 8 域都没有的输入源。(b) **与 StrawberryShake 正面重叠**：官方已有从 schema 生成强类型客户端的 codegen，我们的增量只有「返回 R3 `Observable<T>` 而不是它自己的 `IObservable`」。
- **E2E：A 级**。Hot Chocolate 服务端支持 graphql-ws 协议，只需 `app.UseWebSockets()`（[Hot Chocolate v16 Subscriptions 文档](https://chillicream.com/docs/hotchocolate/v16/defining-a-schema/subscriptions/)，其中「WebSocket (graphql-ws protocol)」小节明确支持 `enisdenjo/graphql-ws` 协议）。仓库已有 WebSocket 域的进程内 server E2E 先例可复用。

### 5.2 入站 Webhook

- **库**：ASP.NET Core（BCL）。
- **形态：契合，但方向反转**。现有 7 域全是「客户端代理」；Webhook 是「服务端接收」，生成物不是 `HttpClient` 代理而是 **endpoint 注册 + 请求体反序列化 + 推入热流**：
  ```csharp
  [Webhooks]
  public interface IGitHubHooks
  {
      [WebhookEndpoint("/hooks/github/push", Secret = "GITHUB_WEBHOOK_SECRET")]
      Observable<PushEvent> Pushes { get; }
  }
  // 用法：app.MapObservableWebhooks<IGitHubHooks>();
  ```
- **E2E：A 级**（`TestServer` + `HttpClient` POST，与 Sse/Grpc 域同构）。
- **判断**：形态上完全可行，但它会引入「服务端域」这一新类别（入口不再是 `For<T>(connection)` 而是 endpoint 映射），是**架构面的扩张**，不是又一个同构域。值得单独立项讨论，不建议顺手加。

---

## 6. 本机与系统边界

### 6.1 ★ FileSystemWatcher

- **库**：BCL `System.IO.FileSystemWatcher`，跨平台。
- **形态：契合**，且声明式收益明显（路径、过滤器、`IncludeSubdirectories`、事件类型全是常量）：
  ```csharp
  [FileWatch]
  public interface IConfigWatcher
  {
      [Watch(@"./config", Filter = "*.json", IncludeSubdirectories = true)]
      Observable<FileSystemEventArgs> Changed { get; }

      [Watch(@"./config", Filter = "*.json", Events = WatchEvents.Renamed)]
      Observable<RenamedEventArgs> Renamed { get; }
  }
  ```
- **E2E：A 级**（临时目录，跨平台）。
- **重叠提醒**：`FileSystemWatcher.Changed` 本身就是 .NET event，**现有 Events 域已经能包装它**。新域的增量只在于「声明式配置 + 生命周期管理 + 内置去抖/合并」——上马前应先确认这个增量是否够一个域的重量。

### 6.2 串口 SerialPort

- **库**：`System.IO.Ports` 10.0.10（2026-07-15，MIT）。
- **形态：契合但缺一块**。`DataReceived` 事件只告诉你「缓冲区里有字节了」，**分帧策略（定长 / 分隔符 / 长度前缀 / 超时切帧）必须由用户提供**——这和 WebSocket 域面对的消息边界问题同构，但串口没有协议层帮忙。
- **E2E：D 级**。要在 CI 里跑必须有虚拟串口对：Windows 需安装 com0com 类驱动，Linux 需 socat/pty，两边方案不同且 Windows 侧需要装驱动。**不适合当前 CI 矩阵。**

### 6.3 MIDI

- **库**：`Melanchall.DryWetMidi` 8.0.3（2025-12-16，MIT；9.0.0 仍在 prerelease，最后预览 2026-05-30）、`NAudio.Midi` 2.3.0（2026-03-13，MIT）。
- **形态：契合**（MIDI 输入设备事件天然是流）。
- **E2E：D 级**（需真实或虚拟 MIDI 设备，Windows 需 loopMIDI 类工具）。

### 6.4 WMI 事件（形态最贴合，但被平台卡死）

- **库**：`System.Management` 10.0.10（2026-07-15，MIT）。
- **形态：契合度是全表最高的之一**。WMI 事件订阅的输入就是**一条 WQL 字符串**，天生适合当特性参数：
  ```csharp
  [Wmi]
  public interface ISystemEvents
  {
      [WmiEvent("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'")]
      Observable<ManagementBaseObject> ProcessStarted { get; }
  }
  ```
  `ManagementEventWatcher` 官方示例正是订阅 `__InstanceCreationEvent`，并说明「可以在示例运行时启动一个进程（如记事本）来测试」（[ManagementEventWatcher 类文档](https://learn.microsoft.com/en-us/dotnet/api/system.management.managementeventwatcher)）——**测试触发器就在进程内**。
- **E2E：A 级，但仅限 Windows**。`System.Management` 只在 Windows 上有实现，会直接打破 Windows + Ubuntu 双矩阵；且部分事件类需要管理员权限。
- **判断**：形态最诱人、平台最尴尬。若要做，必须先决定「允许存在单 OS 域」这条工程策略。

### 6.5 Windows 事件日志

- **库**：`System.Diagnostics.EventLog` 10.0.10（2026-07-15，MIT）。
- **形态：契合**。`EventLogWatcher` 由 `EventLogQuery` 构造，「当有匹配查询条件的事件被写入时触发 `EventRecordWritten`」，还支持 `EventBookmark` 作为起始位点（[EventLogWatcher 类文档](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventing.reader.eventlogwatcher)）。查询是 XPath 字符串，同样适合当特性参数。
- **E2E / 平台**：Windows-only（文档的「Applies to」只列 .NET Framework / Windows Desktop / package-provided）；测试要写入事件通常需要注册事件源（管理员权限）。

### 6.6 ETW / EventPipe

- **库**：`Microsoft.Diagnostics.Tracing.TraceEvent` 3.2.5（2026-07-18，MIT）、`Microsoft.Diagnostics.NETCore.Client` 0.2.661903（2026-01-07，MIT，**版本号仍是 0.x**）。
- **形态：部分**。ETW 内核会话需要管理员权限；EventPipe 的模型是「诊断客户端连到目标进程、配置 provider 集合、拿一条 nettrace 流」，配置面（provider GUID、关键字、级别、缓冲区）远超特性能表达的范围，且本质是**跨进程诊断会话**而非应用内订阅点。
- **E2E**：C/D 级。

### 6.7 ★ .NET 诊断源（EventSource / DiagnosticListener / Activity / Meter）

- **库**：BCL `System.Diagnostics.DiagnosticSource` / `System.Diagnostics.Tracing`。
- **形态：契合，而且有一处天然对齐**：`DiagnosticListener` 的类型声明就是
  `public class DiagnosticListener : DiagnosticSource, IDisposable, IObservable<KeyValuePair<string, object>>`（[DiagnosticListener 类文档](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.diagnosticlistener)）——它已经是 `IObservable`，只是 payload 是弱类型的 `KeyValuePair<string, object>`，且 `IsEnabled` 过滤逻辑要手写。我们的增量非常明确：**把弱类型 payload 变成强类型流**。
  ```csharp
  [Diagnostics]
  public interface IHttpDiagnostics
  {
      [DiagnosticEvent("HttpHandlerDiagnosticListener", "System.Net.Http.HttpRequestOut.Start")]
      Observable<HttpRequestMessage> RequestStarted { get; }

      [EventSourceEvents("System.Net.Http", Level = EventLevel.Informational)]
      Observable<EventWrittenEventArgs> HttpEvents { get; }

      [MeterInstrument("System.Net.Http", "http.client.request.duration")]
      Observable<Measurement<double>> RequestDuration { get; }
  }
  ```
- **E2E：A 级**。纯进程内、跨平台、零外部依赖、零凭据——**在所有候选里工程成本最低**。
- **注意**：`EventSource` / `Meter` / `ActivityListener` / `DiagnosticListener` 是四套不同的机制，域设计要先决定收哪几套，否则 API 面会散。

### 6.8 蓝牙 BLE / HID

- **库**：`InTheHand.BluetoothLE` 4.0.44（2025-12-06，MIT）、`HidSharp` 2.6.4（2025-10-14，包内 license 文件）。
- **形态**：契合（BLE 的 characteristic notification 就是流）。
- **E2E：D 级**。需要真实硬件或平台专用虚拟设备，CI 无法覆盖。**排除级。**

---

## 7. 进程间通信

### 7.1 ★ 命名管道 / Unix domain socket

- **库**：BCL `System.IO.Pipes`（`NamedPipeServerStream` / `NamedPipeClientStream`）与 `System.Net.Sockets`（`UnixDomainSocketEndPoint`；Windows 10 起也支持 AF_UNIX）。
- **形态：契合，但和串口一样缺分帧**。字节流上要做「消息流」必须先定分帧规则。好消息是 WebSocket 域已经解决过一次「二进制/文本消息 → 强类型流」的问题，序列化层可复用 Mqtt/Nats 的 payload serializer 设计（[`docs/design/nats.md`](../design/nats.md) §5）。
  ```csharp
  [Ipc]
  public interface IAgentChannel
  {
      [PipeListen("observables-agent", Framing = Framing.LengthPrefixed)]
      Observable<AgentMessage> Incoming { get; }

      [PipeSend("observables-agent")]
      Observable<Unit> Send(AgentMessage message);
  }
  ```
- **E2E：A 级**（同进程内起 server + client，Windows/Linux 都可）。

### 7.2 D-Bus

- **库**：`Tmds.DBus` 0.94.2（2026-06-17，MIT，**版本号仍是 0.x**）。
- **形态：契合，但已被官方 codegen 占位**。D-Bus signal 天然是流；然而 Tmds.DBus 自带 `dotnet dbus` 代码生成工具（[Tmds.DBus README → docs/tool.md](https://github.com/tmds/Tmds.DBus/blob/main/docs/tool.md)），从 introspection XML 生成 C# 接口——**这正是我们的位置**，重叠度高于 GraphQL。
- **平台**：实际只在 Linux 有意义（session/system bus）。
- **E2E：B 级**（Linux 上可 spawn `dbus-daemon --session`），但 Windows 侧无对应物。

---

## 8. 分布式框架的流抽象

### 8.1 Orleans Streams

- **库**：`Microsoft.Orleans.Server` 10.2.2（2026-07-22，MIT）、`Microsoft.Orleans.TestingHost` 9.2.1（2025-07-17，MIT）。
- **形态：部分**。Orleans 流由 `StreamId`（GUID + 字符串命名空间）标识，通过 stream provider 获取后 `SubscribeAsync`；语义上「流永远存在、订阅生命周期由运行时透明管理」（[Streaming with Orleans](https://learn.microsoft.com/en-us/dotnet/orleans/streaming/)）。卡点：(a) Orleans **自带一整套源生成器与序列化标注**（`[GenerateSerializer]` 等），我们的生成器要和它共存；(b) 消费端主要在 grain 内部，grain 本身就有 Orleans 的编程模型约束；(c) 投递保证随 provider 而变（SMS 尽力一次 vs Azure Queue 至少一次），`Observable<T>` 会抹平这个差异。
- **E2E：A 级**。`Microsoft.Orleans.TestingHost` 提供 `InProcessTestCluster`（推荐）与 `TestCluster`，官方明确「both … use the same underlying **in-process** silo host by default」（[Unit testing with Orleans](https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/testing)）。
- **判断**：可测性极好，但**价值存疑**——Orleans 用户已经在框架内，多一层生成器代理收益不明显。

### 8.2 Akka.NET EventStream

- **库**：`Akka` 1.5.70（2026-07-03，Apache-2.0）、`Akka.Streams` 同版本。
- **形态：部分，价值重叠**。`system.EventStream.Subscribe(actorRef, typeof(DeadLetter))` 是按**消息类型**订阅，且订阅者必须是 `IActorRef`（[Akka.NET Event Bus 文档](https://getakka.net/articles/utilities/event-bus.html)）。我们可以生成一个桥接 actor 把消息推进 `Observable<T>`，但 Akka.NET 已有 `Akka.Streams`（Reactive Streams 实现），用户要反应式接口时会先用它。
- **E2E：A 级**（`ActorSystem` 就在进程内）。
- **判断**：技术可行、价值低。

### 8.3 Dapr pub/sub

- **库**：`Dapr.Client` / `Dapr.AspNetCore` 1.18.5（2026-07-25，Apache-2.0）。
- **形态：不契合**。Dapr 的订阅有三种方式（declarative / streaming / programmatic），主流两种都**不在客户端**：declarative 是一份 `subscription.yaml`，把 topic 路由到应用的 HTTP endpoint；programmatic 是在 controller 方法上打 `[Topic("order-pub-sub", "orders")] [HttpPost("checkout")]`，由 sidecar 回调进来，返回 `200 OK` 才算 ack（[How to: Publish & subscribe](https://docs.dapr.io/developing-applications/building-blocks/pubsub/howto-publish-subscribe/)）。也就是说：**订阅是服务端 endpoint 声明**，而且 `[Topic]` 特性已经是 Dapr 自己的方案，我们做不出增量。
- **E2E：C 级**。所有示例都以 `dapr run --app-id …` 启动，需要 daprd sidecar 二进制 + 一份 `pubsub.yaml` 组件（默认还会拉起 Redis 容器）。
- **结论**：明确不建议。唯一有讨论价值的是 Dapr 的 **streaming subscription**（在用户代码里定义、不经 endpoint），但那已是 Dapr SDK 提供的流式 API，包装收益也很薄。

---

## 9. 工业与 IoT

### 9.1 ★ OPC UA

- **库**：`OPCFoundation.NetStandard.Opc.Ua` 1.5.378.156（2026-07-10）。**许可证是「OPC Foundation MIT License 1.00」**——仓库 `LICENSE.txt` 的正文即 MIT 文本，README 也写「The project is licensed under the OPC Foundation MIT License」（[UA-.NETStandard LICENSE.txt](https://github.com/OPCFoundation/UA-.NETStandard/blob/master/LICENSE.txt)、[README](https://github.com/OPCFoundation/UA-.NETStandard/blob/master/README.md)）。历史上 OPC 生态有 RCL/GPL 双授权的传闻，就当前 master 的 LICENSE 而言是 MIT；采用前仍建议复核发布包内的 license 文件。替代品 `Workstation.UaClient` 3.2.3 最后发布 **2024-02-14**，相对停滞。
- **形态：契合度很高**。OPC UA 的 MonitoredItem 订阅参数（NodeId、采样间隔、死区、队列大小）全是常量，天生适合特性：
  ```csharp
  [OpcUa]
  public interface IPlcTags
  {
      [OpcSubscribe("ns=2;s=Boiler.Temperature", SamplingInterval = 250)]
      Observable<DataValue> Temperature { get; }

      [OpcSubscribeEvent("ns=0;i=2253")]           // Server 对象的事件通知
      Observable<OpcEvent> ServerEvents { get; }

      [OpcCall("ns=2;s=Boiler.Start")]
      Observable<Unit> Start();
  }
  ```
- **E2E：A/B 级**。UA-.NETStandard 同时提供 client 与 server 侧栈，理论上可以在测试进程内起一个最小 UA server（该仓库自身的参考服务器示例即基于同一套库）。需要实测「在测试进程内 host 一个 server 并完成证书握手」的工程量——OPC UA 的**证书与安全策略**是最可能拖慢 E2E 的部分（可用 `SecurityPolicy.None` 简化）。
- **凭据**：否。
- **判断**：这是「形态契合 + 生态里几乎没有反应式封装 + 目标用户明确（工业）」三者少见的交集，值得单列评估。

### 9.2 CoAP Observe

- **库**：`CoAPnet` 1.2.0（**2022-04-30**，MIT，作者与 MQTTnet 同为 chkr1011）。
- **形态：契合**。CoAP 的 Observe（RFC 7641）字面意义就是「观察一个资源」，README 的特性列表里明确列有「Observe (RFC 7641)」（[chkr1011/CoAPnet README](https://github.com/chkr1011/CoAPnet/blob/master/README.md)）。
- **E2E：D 级 + 维护风险**。仓库 `Source/` 下只有 `CoAPnet`、`CoAPnet.Extensions.DTLS`、`CoAP.TestClient`、`CoAPnet.Tests` 四个项目（GitHub Contents API，2026-07-27）——**没有 server 实现**，无法照搬 Mqtt 域的「进程内 broker」打法。加上最后发布已是 2022 年，风险偏高。
- **结论**：形态很美，工程条件不成熟。

---

## 10. 其他

### 10.1 IMAP IDLE

- **库**：`MailKit` 4.17.0（2026-05-27，MIT），README 的能力列表包含 [IDLE (RFC 2177)](https://github.com/jstedfast/MailKit/blob/master/README.md)。
- **形态：部分**。IDLE 需要周期性重发命令（服务端通常 ~29 分钟超时）、独占一条连接、且退出 IDLE 才能执行其它命令——「连接生命周期与订阅生命周期强耦合」与 PostgreSQL LISTEN 同类，但更复杂。
- **E2E：D 级**。.NET 生态没有可嵌入的 IMAP server（`SmtpServer` 11.1.0 / 2025-11-17 只覆盖 SMTP），测试只能连真实邮箱 → **需要凭据**。

---

## 11. 明确排除的方向及原因

| 方向 | 排除原因 |
|------|----------|
| **gRPC-Web** | 不是新边界，只是 Grpc 的传输层。`Grpc.Net.Client.Web` 2.80.0（2026-05-01，Apache-2.0）提供的是一个 `GrpcWebHandler`，套在现有 `GrpcChannel` 上即可；对已有 Grpc 域而言是「多一个配置项」，不该成为第 9 个域，更不该产出 2 个新包。 |
| **MassTransit / NServiceBus / Rebus** | 它们是**消息框架**（自带 consumer 抽象、DI 集成、saga、重试策略），不是 IO 边界；在其之上再套接口代理是抽象层打架。另外 `MassTransit` 9.1.2（2026-06-03）的 NuGet licenseUrl 指向 `https://massient.com/license`（非 SPDX 表达式，需人工核对商业条款）。`Rebus` 8.9.2（2026-04-17，MIT）同理属于框架层。 |
| **Discord.Net / Telegram.Bot 等 SDK 网关** | 这些 SDK 对外暴露的就是 .NET event，**Events 域已经覆盖**；且 E2E 必须持有真实 bot token（凭据）。 |
| **WebRTC DataChannel（`SIPSorcery` 10.0.12 / 2026-07-13 / BSD-3-Clause）** | 连接建立需要信令通道 + SDP 协商 + ICE 候选交换，这是一段有状态协商流程，不能用「打特性即订阅」表达；测试还需要两端 + 信令服务器。 |
| **Modbus（NModbus 等）** | 主站-从站轮询式寄存器读写，**服务端不推送**，做成 `Observable` 只是把 `Timer` 藏起来。 |
| **全局键鼠钩子** | Windows-only、需要消息循环、且属于安全敏感 API（易被杀软误报）。 |
| **`System.Threading.Channels` / `IAsyncEnumerable`** | 进程内数据结构，不是 IO 边界；R3 与 System.Reactive 都已有现成转换（`ToObservable`），零增量。 |
| **Azure Event Grid** | 投递方式是 HTTP push 到 webhook，与候选 #19（入站 Webhook）重合，不单列。 |
| **Amazon Kinesis（`AWSSDK.Kinesis` 4.0.100.6 / 2026-07-23 / Apache-2.0）** | 形态判定与 Kafka 完全一致（分片 + 迭代器位点 + 显式 checkpoint），E2E 同样落到 LocalStack/Docker，单列没有新信息。 |
| **Apache ActiveMQ / Artemis（`Apache.NMS.AMQP` 2.4.0 / 2025-08-24）** | 其协议面是 AMQP 1.0，已被候选 #3 覆盖；NMS 是 JMS 风格的另一套抽象，叠加只会增加维护面。 |
| **STOMP / XMPP** | .NET 生态里没有维护活跃、被广泛使用的客户端库，用户基数不足以支撑一个域（2 个 NuGet 包 + 双生成器 + E2E 的固定成本）。 |
| **SQLite update hook** | `Microsoft.Data.Sqlite` 未暴露该回调（需下沉到 `SQLitePCLRaw` 原始 API）；且它是**同进程内同一个库**的回调，不构成 IO 边界。 |
| **Windows 注册表变更通知 / USB 设备到达** | 都可以用 WQL 表达，被候选 #23（WMI）覆盖，不单列。 |
| **Azure Storage Queues** | 与 SQS 同为轮询 + 显式删除模型；本地 Azurite 模拟器是 Node/Docker 工具，E2E 条件比 Service Bus 更差。 |

---

## 12. 结论速览

- **第一梯队（形态契合 + E2E ≤ B 级 + 无凭据）**：Redis Pub/Sub（Garnet 进程内）、AMQP 1.0（AMQPNetLite listener 进程内）、.NET 诊断源、PostgreSQL LISTEN/NOTIFY、MongoDB Change Streams、GraphQL Subscriptions、ZeroMQ（**LGPL 需先决策**）、OPC UA、FileSystemWatcher、命名管道 / UDS。
- **形态诱人但被平台卡死**：WMI（WQL 就是完美的特性参数，但 Windows-only）。
- **热门但形态不契合，建议明确写进「不做」清单**：Cosmos DB Change Feed（租约容器 + 批处理 delegate）、Dapr pub/sub（sidecar + 服务端 endpoint）、Redis Streams（轮询 + 游标 + ack）、PostgreSQL 逻辑复制（slot + LSN）。
- **需要新增 CI 依赖（Docker / JVM / 云 CLI）才能测**：Kafka、RabbitMQ、Pulsar、Service Bus、Event Hubs、SQS/SNS、Google Pub/Sub、SQL Server、EventStore/Kurrent。这批的共同门槛不是形态，而是**仓库至今没有任何 E2E 依赖 Docker** 这一工程约束。

## 13. 参考来源清单

NuGet 元数据（版本 / 发布日 / 许可证，2026-07-27 取自 `https://api.nuget.org/v3/registration5-semver1/<id>/index.json`）不再逐条列出，正文表格已标注。其余一手来源：

- Confluent .NET client — <https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/README.md>
- Apache Kafka（Java 依赖）— <https://github.com/apache/kafka/blob/trunk/README.md>
- RabbitMQ .NET API Guide — <https://www.rabbitmq.com/client-libraries/dotnet-api-guide>
- AMQP.Net Lite README（listener / broker）— <https://github.com/Azure/amqpnetlite/blob/master/README.md>
- Azure Service Bus emulator — <https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator>
- Azure Event Hubs emulator — <https://learn.microsoft.com/en-us/azure/event-hubs/overview-emulator>
- Google Cloud Pub/Sub emulator — <https://docs.cloud.google.com/pubsub/docs/emulator>
- StackExchange.Redis Pub/Sub 顺序 — <https://stackexchange.github.io/StackExchange.Redis/PubSubOrder.html>
- StackExchange.Redis Streams — <https://stackexchange.github.io/StackExchange.Redis/Streams>
- Microsoft Garnet README — <https://github.com/microsoft/garnet/blob/main/README.md>
- Garnet `GarnetServer` 源码 — <https://github.com/microsoft/garnet/blob/main/libs/host/GarnetServer.cs>
- NetMQ README（LGPLv3）— <https://github.com/zeromq/netmq/blob/master/README.md>
- DotPulsar README — <https://github.com/apache/pulsar-dotpulsar/blob/master/README.md>
- Npgsql Waiting for Notifications — <https://www.npgsql.org/doc/wait.html>
- SqlDependency — <https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/detecting-changes-with-sqldependency>
- Enabling Query Notifications（Service Broker）— <https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/enabling-query-notifications>
- MongoDB Change Streams（副本集要求）— <https://www.mongodb.com/docs/manual/changestreams/>
- EphemeralMongo README — <https://github.com/asimmon/ephemeral-mongo/blob/main/README.md>
- Cosmos DB change feed processor — <https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/change-feed-processor>
- Cosmos DB emulator — <https://learn.microsoft.com/en-us/azure/cosmos-db/emulator>
- graphql-ws 协议规范 — <https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md>
- Hot Chocolate v16 Subscriptions — <https://chillicream.com/docs/hotchocolate/v16/defining-a-schema/subscriptions/>
- ManagementEventWatcher — <https://learn.microsoft.com/en-us/dotnet/api/system.management.managementeventwatcher>
- EventLogWatcher — <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventing.reader.eventlogwatcher>
- DiagnosticListener（`IObservable` 实现）— <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.diagnosticlistener>
- Tmds.DBus README / dotnet dbus 工具 — <https://github.com/tmds/Tmds.DBus/blob/main/docs/tool.md>
- Orleans Streaming — <https://learn.microsoft.com/en-us/dotnet/orleans/streaming/>
- Orleans 单元测试（`InProcessTestCluster`）— <https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/testing>
- Akka.NET Event Bus — <https://getakka.net/articles/utilities/event-bus.html>
- Dapr pub/sub How-to — <https://docs.dapr.io/developing-applications/building-blocks/pubsub/howto-publish-subscribe/>
- UA-.NETStandard LICENSE / README — <https://github.com/OPCFoundation/UA-.NETStandard/blob/master/LICENSE.txt>
- CoAPnet README / 仓库结构 — <https://github.com/chkr1011/CoAPnet/blob/master/README.md>
- MailKit README（IDLE / RFC 2177）— <https://github.com/jstedfast/MailKit/blob/master/README.md>
