# 候选边界生态采用度证据

> 取数日期：**2026-07-27**。
> 范围：形态白名单中的 17 个“契合”候选；本文不重新评估形态 gate，也不做最终 top-N 决策。

## 1. 取数口径与方法

- **NuGet**：使用 NuGet 官方搜索 API 的 `totalDownloads`、当前稳定版本和版本列表；总下载量是包的累计下载量，不是活跃安装数。API 查询结果是取数日快照，后续会继续变化。NuGet 搜索 API 示例：[StackExchange.Redis](https://azuresearch-usnc.nuget.org/query?q=packageid%3AStackExchange.Redis&prerelease=false&semVerLevel=2.0)、[System.Diagnostics.DiagnosticSource](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Diagnostics.DiagnosticSource&prerelease=false&semVerLevel=2.0)。
- **近 12 个月下载趋势**：对本次查询到的 NuGet 官方 payload，**不可得**。该 payload 提供累计总量和按版本累计量，但没有可复核的按月时间序列；因此不把当前版本累计下载量伪装成 12 个月趋势。
- **GitHub**：使用 GitHub 官方 REST API 的 `stargazers_count`、`forks_count`、`pushed_at`，以及 `releases/latest` 的稳定 release。`pushed_at` 只表示默认分支最近推送，不等于最近一次功能提交或提交数量。
- **Stack Overflow**：使用 Stack Exchange 官方 API 的 `tags/{tags}/info` 的标签问题数；这是标签级总量，不是 .NET 子集，也不等于某个 NuGet 包的用户数。未将搜索引擎结果计入。统一取数接口：[标签统计 API](https://api.stackexchange.com/2.3/tags/amqp;redis;zeromq;postgresql;graphql;webhooks;filesystemwatcher;serial-port;midi;wmi;event-log;eventsource;diagnosticlistener;activity;bluetooth;named-pipes;unix-domain-sockets;dbus;opc-ua;coap/info?site=stackoverflow&filter=default)。
- **许可证**：包版本与许可证字段以 NuGet 官方包页/API 为准；GitHub 仓库许可证只作为维护仓库的辅助交叉检查。包内 license 文件、`NOASSERTION` 或多重许可证均保留为风险提示。
- **活跃度**：将“最近 release”和“最近 push”并列记录；没有 release 的仓库不会用最近 commit 替代 release。

候选清单严格采用本 ticket 的 17 项。候选普查中的 **MongoDB Change Streams 属于“部分”形态，明确排除，不列入下表**；旧摘要把它列入第一梯队是清单矛盾，不是本文范围。

## 2. 总览

### 2.1 NuGet、GitHub 与 Stack Overflow

| 候选 | 主要 .NET 包（稳定版本；许可证） | NuGet 累计下载量（取数日） | 近 12 个月趋势 | 官方 GitHub 活跃度：stars / forks；release；最近 push | SO 标签量级 | 关键限制 / 解释 |
|---|---|---:|---|---|---:|---|
| AMQP 1.0 | `AMQPNetLite` 2.5.3；Apache-2.0 | 10,965,963 | 不可得 | [417 / 153](https://api.github.com/repos/Azure/amqpnetlite)；[v2.5.3，2026-06-02](https://api.github.com/repos/Azure/amqpnetlite/releases/latest)；push 2026-07-13 | `amqp` 2,531 | 协议生态成熟，但 .NET 包量级明显低于 Redis/PostgreSQL；listener/broker 能力和 client API 的跨平台覆盖需分别验证。[README](https://github.com/Azure/amqpnetlite/blob/master/README.md) |
| Redis Pub/Sub | `StackExchange.Redis` 3.0.17；MIT | 1,120,180,867 | 不可得 | [6,188 / 1,558](https://api.github.com/repos/StackExchange/StackExchange.Redis)；[3.0.17，2026-07-10](https://api.github.com/repos/StackExchange/StackExchange.Redis/releases/latest)；push 2026-07-27 | `redis` 25,593 | 采用度极高且语义接近纯推送；总量包含 Redis 全部 client 能力，不只 Pub/Sub。[Pub/Sub 文档](https://stackexchange.github.io/StackExchange.Redis/PubSubOrder.html) |
| ZeroMQ | `NetMQ` 4.0.4.2；LGPLv3 | 11,310,204 | 不可得 | [3,171 / 764](https://api.github.com/repos/zeromq/netmq)；[4.0.4.2，2026-05-24](https://api.github.com/repos/zeromq/netmq/releases/latest)；push 2026-06-18 | `zeromq` 3,343 | brokerless、模式丰富；运行时依赖链的 LGPLv3 是独立许可证决策点。[许可证说明](https://github.com/zeromq/netmq/blob/master/README.md) |
| PostgreSQL LISTEN/NOTIFY | `Npgsql` 10.0.3；PostgreSQL License | 910,641,383 | 不可得 | [3,714 / 889](https://api.github.com/repos/npgsql/npgsql)；[v10.0.3，2026-05-27](https://api.github.com/repos/npgsql/npgsql/releases/latest)；push 2026-07-23 | `postgresql` 179,928 | 生态很大；通知在请求周期外需要专用连接持续 `Wait`，不能简单等同普通连接池查询。[Npgsql 文档](https://www.npgsql.org/doc/wait.html) |
| GraphQL Subscriptions | `HotChocolate.AspNetCore` 16.5.1；MIT；`StrawberryShake.Transport.WebSockets` 16.5.1；MIT | 54,251,358；12,279,599 | 不可得 | [5,731 / 807](https://api.github.com/repos/ChilliCream/graphql-platform)；[16.5.1，2026-07-22](https://github.com/ChilliCream/graphql-platform/releases/tag/16.5.1)；push 2026-07-27 | `graphql` 20,907 | 生态活跃，但 Hot Chocolate/Strawberry Shake 已有 schema/codegen，新增域会有官方方案重叠。[平台仓库](https://github.com/ChilliCream/graphql-platform) |
| 入站 Webhook | ASP.NET Core shared framework `Microsoft.AspNetCore.App`；MIT；无独立候选包 | 不适用 | 不适用 | [38,241 / 10,841](https://api.github.com/repos/dotnet/aspnetcore)；[v8.0.29，2026-07-14](https://api.github.com/repos/dotnet/aspnetcore/releases/latest)；push 2026-07-27 | `webhooks` 4,945 | 采用的是 HTTP/ASP.NET Core 生态，不是一个可由单一 NuGet 包代表的 webhook client；生成物方向是服务端 endpoint。[ASP.NET Core 仓库](https://github.com/dotnet/aspnetcore) |
| FileSystemWatcher | `System.IO.FileSystem.Watcher` 4.3.0；MIT | 344,628,116 | 不可得 | [.NET runtime 18,115 / 5,528](https://api.github.com/repos/dotnet/runtime)；[v10.0.10，2026-07-15](https://api.github.com/repos/dotnet/runtime/releases/latest)；push 2026-07-27 | `filesystemwatcher` 1,239 | 包下载量很高，但包含 BCL/传递依赖采用；该能力本身已是 CLR event，和现有 Events 域有重叠。[API](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher) |
| SerialPort | `System.IO.Ports` 10.0.10；MIT | 116,910,143 | 不可得 | [.NET runtime 18,115 / 5,528](https://api.github.com/repos/dotnet/runtime)；[v10.0.10，2026-07-15](https://api.github.com/repos/dotnet/runtime/releases/latest)；push 2026-07-27 | `serial-port` 11,694 | `DataReceived` 只表示有字节到达，不提供消息分帧；跨平台 E2E 通常仍需硬件或虚拟串口。[API](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport) |
| MIDI | `NAudio` 2.3.0；MIT；`Melanchall.DryWetMidi` 8.0.3；MIT | 12,594,017；254,941 | 不可得 | NAudio [6,188 / 1,130](https://api.github.com/repos/naudio/NAudio)，[v2.3.0，2026-03-12](https://api.github.com/repos/naudio/NAudio/releases/latest)，push 2026-07-25；DryWetMIDI [691 / 83](https://api.github.com/repos/melanchall/drywetmidi)，[v8.0.3，2025-12-16](https://api.github.com/repos/melanchall/drywetmidi/releases/latest)，push 2026-07-24 | `midi` 2,005 | NAudio 的总量较高但不全是 MIDI；DryWetMIDI 更专门但规模小；真实设备/虚拟设备是主要测试限制。[NAudio](https://github.com/naudio/NAudio)、[DryWetMIDI](https://github.com/melanchall/drywetmidi) |
| WMI 事件 | `System.Management` 10.0.10；MIT | 549,131,032 | 不可得 | [.NET runtime 18,115 / 5,528](https://api.github.com/repos/dotnet/runtime)；[v10.0.10，2026-07-15](https://api.github.com/repos/dotnet/runtime/releases/latest)；push 2026-07-27 | `wmi` 4,374 | BCL 下载量很高，但能力是 Windows-only；WQL 很适合声明式特性，平台范围限制明显。[API](https://learn.microsoft.com/en-us/dotnet/api/system.management.managementeventwatcher) |
| Windows 事件日志 | `System.Diagnostics.EventLog` 10.0.10；MIT | 2,418,967,561 | 不可得 | [.NET runtime 18,115 / 5,528](https://api.github.com/repos/dotnet/runtime)；[v10.0.10，2026-07-15](https://api.github.com/repos/dotnet/runtime/releases/latest)；push 2026-07-27 | `event-log` 1,277 | 下载量受 Windows/BCL 传递依赖放大；Windows-only，bookmark 与事件源权限会进入 API。[API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventing.reader.eventlogwatcher) |
| .NET 诊断源 | `System.Diagnostics.DiagnosticSource` 10.0.10；MIT；EventSource/Meter 为 .NET 诊断栈 | 9,777,937,683 | 不可得 | [.NET runtime 18,115 / 5,528](https://api.github.com/repos/dotnet/runtime)；[v10.0.10，2026-07-15](https://api.github.com/repos/dotnet/runtime/releases/latest)；push 2026-07-27 | `eventsource` 2,110；`DiagnosticListener` 无标签 | 采用度代理指标最高，但总量含大量框架/遥测传递依赖；`Activity` 标签语义过宽，未当作 .NET 诊断数。[DiagnosticListener API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.diagnosticlistener) |
| BLE / HID | `InTheHand.BluetoothLE` 4.0.44；MIT；`HidSharp` 2.6.4；包内 license | 180,142；1,490,685 | 不可得 | 32feet [982 / 225](https://api.github.com/repos/inthehand/32feet)，release 4.0（2017-10-11），push 2026-07-20；HidSharp fork [206 / 48](https://api.github.com/repos/IntergatedCircuits/HidSharp)，无 release，push 2026-07-02 | `bluetooth` 17,326 | 标签量大于专用 .NET 包量；设备、驱动、平台权限使采用和 E2E 不可只看下载量。[32feet](https://github.com/inthehand/32feet)、[HidSharp](https://github.com/IntergatedCircuits/HidSharp) |
| 命名管道 / Unix domain socket | `System.IO.Pipes` 4.3.0；MIT；`System.Net.Sockets` 4.3.0；MIT | 172,784,595；2,356,850,399 | 不可得 | [.NET runtime 18,115 / 5,528](https://api.github.com/repos/dotnet/runtime)；[v10.0.10，2026-07-15](https://api.github.com/repos/dotnet/runtime/releases/latest)；push 2026-07-27 | `named-pipes` 1,895；`unix-socket` 730 | 包下载量是通用 IPC/socket 使用量，不能拆出 UDS 或命名管道；字节流仍需额外 framing。[NamedPipe API](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes)、[Unix socket API](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.unixdomainsocketendpoint) |
| D-Bus | `Tmds.DBus` 0.94.2；MIT | 5,037,742 | 不可得 | [351 / 58](https://api.github.com/repos/tmds/Tmds.DBus)；[0.94.2，2026-06-17](https://api.github.com/repos/tmds/Tmds.DBus/releases/latest)；push 2026-07-11 | `dbus` 1,466 | Linux 生态、0.x 版本；仓库自带 generator/tool，和新增接口生成器的价值重叠。[generator 文档](https://github.com/tmds/Tmds.DBus/blob/main/docs/tool.md) |
| OPC UA | `OPCFoundation.NetStandard.Opc.Ua` 1.5.378.156；OPC Foundation MIT License | 5,361,074 | 不可得 | [2,358 / 1,054](https://api.github.com/repos/OPCFoundation/UA-.NETStandard)；[1.5.378.156，2026-07-10](https://api.github.com/repos/OPCFoundation/UA-.NETStandard/releases/latest)；push 2026-07-27 | `opc-ua` 1,056 | 专业工业生态而非大众 .NET 生态；官方仓库和 NuGet 仍活跃，但证书/安全策略会增加实现与测试成本。[许可证](https://github.com/OPCFoundation/UA-.NETStandard/blob/master/LICENSE.txt) |
| CoAP Observe | `CoAPnet` 1.2.0；MIT | 75,888 | 不可得 | [74 / 18](https://api.github.com/repos/chkr1011/CoAPnet)；[v1.2.0，2022-04-30](https://api.github.com/repos/chkr1011/CoAPnet/releases/latest)；push 2022-10-16 | `coap` 233 | 形态与 Observe 很吻合，但 .NET 包与 GitHub 规模小，官方仓库多年未 push；维护风险不能由协议成熟度替代。[README](https://github.com/chkr1011/CoAPnet/blob/master/README.md) |

### 2.2 指标解读

- **高采用度但不等于高优先级**：`System.Diagnostics.DiagnosticSource`、`System.Diagnostics.EventLog`、`System.Net.Sockets`、`StackExchange.Redis`、`Npgsql` 的累计下载量很高；其中 BCL 包和基础设施包的总量明显会被传递依赖、SDK 资产和框架安装放大，不能直接解释为该候选边界的独立需求。[NuGet DiagnosticSource](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Diagnostics.DiagnosticSource&prerelease=false&semVerLevel=2.0)、[NuGet EventLog](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Diagnostics.EventLog&prerelease=false&semVerLevel=2.0)、[NuGet Sockets](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Net.Sockets&prerelease=false&semVerLevel=2.0)。
- **高采用度、但产品边界可能重叠**：GraphQL 的官方平台已有 server/client codegen；FileSystemWatcher、EventLog、SerialPort、DiagnosticSource 都直接暴露 .NET API 或 event/observable 机制。高下载量应与“Observables 是否提供足够新增价值”分开评估。[Hot Chocolate/Strawberry Shake 仓库](https://github.com/ChilliCream/graphql-platform)、[FileSystemWatcher API](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)、[DiagnosticListener API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.diagnosticlistener)。
- **形态契合但生态较小**：AMQPNetLite、NetMQ、Tmds.DBus、OPC UA、CoAPnet 的 .NET 包累计量从约 500 万到约 1,100 万不等，不能与 Redis/Npgsql 的百亿级或十亿级累计量直接横比；OPC UA 的仓库活跃度明显强于 CoAPnet。[NuGet AMQPNetLite](https://azuresearch-usnc.nuget.org/query?q=packageid%3AAMQPNetLite&prerelease=false&semVerLevel=2.0)、[NuGet CoAPnet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ACoAPnet&prerelease=false&semVerLevel=2.0)。
- **平台 / 硬件是采用度之外的成本**：WMI、Windows 事件日志依赖 Windows；BLE/HID、MIDI、SerialPort 的真实端到端路径依赖设备或驱动；D-Bus 的主要运行环境是 Linux。这些事实会改变测试矩阵和可交付性，但本文不据此替代后续 gating ticket。[WMI API](https://learn.microsoft.com/en-us/dotnet/api/system.management.managementeventwatcher)、[EventLogWatcher API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventing.reader.eventlogwatcher)、[32feet](https://github.com/inthehand/32feet)、[DryWetMIDI](https://github.com/melanchall/drywetmidi)、[Tmds.DBus](https://github.com/tmds/Tmds.DBus)。
- **官方维护信号分化**：StackExchange.Redis、Npgsql、ChilliCream、.NET runtime、OPC UA 在取数日前仍有近期 push/release；CoAPnet 的稳定 release 停在 2022-04-30 且最近 push 为 2022-10-16；32feet 虽有近期 push，但其 GitHub latest release 仍是 2017 年的 4.0。[GitHub CoAPnet API](https://api.github.com/repos/chkr1011/CoAPnet)、[GitHub 32feet API](https://api.github.com/repos/inthehand/32feet)、[GitHub Npgsql API](https://api.github.com/repos/npgsql/npgsql)。

## 3. 分候选证据详情

以下每节保留“采用度证据 + 成熟度/限制”，而不是重新给出最终排序。

### 3.1 AMQP 1.0

- **包与规模**：`AMQPNetLite` 2.5.3，累计 10,965,963；NuGet API 的包描述明确该库同时包含 client 和 listener。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3AAMQPNetLite&prerelease=false&semVerLevel=2.0)
- **官方维护**：`Azure/amqpnetlite` 为 417 stars、153 forks；官方 API 返回最近 push 2026-07-13，latest release `v2.5.3` 发布于 2026-06-02。[仓库 API](https://api.github.com/repos/Azure/amqpnetlite)、[release API](https://api.github.com/repos/Azure/amqpnetlite/releases/latest)
- **采用度旁证**：Stack Overflow `amqp` 标签 2,531 个问题；这是 AMQP 家族标签，不专指 AMQP 1.0 或 .NET。[SO API](https://api.stackexchange.com/2.3/tags/amqp/info?site=stackoverflow&filter=default)
- **限制**：AMQP 1.0 的协议生态成熟，但单一 .NET 包下载量不大；listener 能否在 Windows 与 Ubuntu 的测试矩阵中稳定充当最小 broker，应在 E2E ticket 中验证，不能仅凭 README 的能力声明下结论。[官方 README](https://github.com/Azure/amqpnetlite/blob/master/README.md)

### 3.2 Redis Pub/Sub

- **包与规模**：`StackExchange.Redis` 3.0.17，累计 1,120,180,867，MIT；NuGet API 标记为 verified 包。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3AStackExchange.Redis&prerelease=false&semVerLevel=2.0)
- **官方维护**：官方仓库 6,188 stars、1,558 forks，最近 push 2026-07-27；latest stable release 3.0.17 于 2026-07-10 发布。[仓库 API](https://api.github.com/repos/StackExchange/StackExchange.Redis)、[release API](https://api.github.com/repos/StackExchange/StackExchange.Redis/releases/latest)
- **采用度旁证**：`redis` 标签 25,593 个问题。[SO API](https://api.stackexchange.com/2.3/tags/redis/info?site=stackoverflow&filter=default)
- **限制**：StackExchange.Redis 的下载量代表整个 Redis client（缓存、数据结构、Streams 等），不是 Pub/Sub 单项；但官方 Pub/Sub 文档所描述的 Subscribe/OnMessage 是直接的推送 API，和本产品形态的映射成本较低。[Pub/Sub 顺序文档](https://stackexchange.github.io/StackExchange.Redis/PubSubOrder.html)、[Redis Pub/Sub 官方文档](https://redis.io/docs/latest/develop/pubsub/)

### 3.3 ZeroMQ

- **包与规模**：`NetMQ` 4.0.4.2，累计 11,310,204；仓库 README 以 LGPLv3 描述项目，NuGet license URL 指向 `COPYING.LESSER`。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ANetMQ&prerelease=false&semVerLevel=2.0)、[许可证](https://github.com/zeromq/netmq/blob/master/COPYING.LESSER)
- **官方维护**：仓库 3,171 stars、764 forks，最近 push 2026-06-18；4.0.4.2 于 2026-05-24 发布。[仓库 API](https://api.github.com/repos/zeromq/netmq)、[release API](https://api.github.com/repos/zeromq/netmq/releases/latest)
- **采用度旁证**：`zeromq` 标签 3,343 个问题。[SO API](https://api.stackexchange.com/2.3/tags/zeromq/info?site=stackoverflow&filter=default)
- **限制**：ZeroMQ 是 brokerless，测试可使用 `inproc`/本机 transport；但 PUB/SUB、REQ/REP、DEALER/ROUTER 等 socket 模式不是同一语义，域设计必须先限制支持子集。LGPLv3 需要在包分发前单独复核。[NetMQ README](https://github.com/zeromq/netmq/blob/master/README.md)、[ZeroMQ 官方指南](https://zguide.zeromq.org/)

### 3.4 PostgreSQL LISTEN/NOTIFY

- **包与规模**：`Npgsql` 10.0.3，累计 910,641,383，PostgreSQL License。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ANpgsql&prerelease=false&semVerLevel=2.0)
- **官方维护**：仓库 3,714 stars、889 forks，最近 push 2026-07-23；v10.0.3 于 2026-05-27 发布。[仓库 API](https://api.github.com/repos/npgsql/npgsql)、[release API](https://api.github.com/repos/npgsql/npgsql/releases/latest)
- **采用度旁证**：`postgresql` 标签 179,928 个问题，是本候选中最大的 Stack Overflow 标签之一；它覆盖整个 PostgreSQL 生态。[SO API](https://api.stackexchange.com/2.3/tags/postgresql/info?site=stackoverflow&filter=default)
- **限制**：Npgsql 官方文档说明连接在正常交互中处理通知；要在请求周期外持续收到通知，需持续 `Wait`/异步 Wait，因此订阅会占用专用连接并与连接生命周期绑定。[Npgsql Waiting for Notifications](https://www.npgsql.org/doc/wait.html)、[PostgreSQL NOTIFY](https://www.postgresql.org/docs/current/sql-notify.html)

### 3.5 GraphQL Subscriptions

- **包与规模**：`HotChocolate.AspNetCore` 16.5.1 累计 54,251,358；`StrawberryShake.Transport.WebSockets` 16.5.1 累计 12,279,599；两者 NuGet 元数据均为 MIT。[Hot Chocolate NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3AHotChocolate.AspNetCore&prerelease=false&semVerLevel=2.0)、[Strawberry Shake NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3AStrawberryShake.Transport.WebSockets&prerelease=false&semVerLevel=2.0)
- **官方维护**：ChilliCream 仓库 5,731 stars、807 forks，最近 push 2026-07-27；稳定 release 16.5.1 于 2026-07-22 发布，随后仍有 16.6.0 prerelease。[仓库 API](https://api.github.com/repos/ChilliCream/graphql-platform)、[稳定 release](https://github.com/ChilliCream/graphql-platform/releases/tag/16.5.1)
- **采用度旁证**：`graphql` 标签 20,907 个问题。[SO API](https://api.stackexchange.com/2.3/tags/graphql/info?site=stackoverflow&filter=default)
- **限制**：官方平台同时覆盖 Hot Chocolate server 和 Strawberry Shake client，并已有 GraphQL schema/codegen；新增 Observables 域的差异主要是接口形状与返回流类型，官方方案重叠度高。[平台仓库](https://github.com/ChilliCream/graphql-platform)、[Hot Chocolate subscriptions](https://chillicream.com/docs/hotchocolate/v16/defining-a-schema/subscriptions/)、[graphql-ws 协议](https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md)

### 3.6 入站 Webhook

- **包与规模**：ASP.NET Core 是 shared framework `Microsoft.AspNetCore.App`，没有能代表“入站 webhook 采用度”的独立 NuGet 包；因此 NuGet 总下载量标记为**不适用**。ASP.NET Core 仓库许可证为 MIT。[官方仓库](https://github.com/dotnet/aspnetcore)
- **官方维护**：仓库 38,241 stars、10,841 forks，最近 push 2026-07-27；GitHub latest release API 返回 v8.0.29，发布时间 2026-07-14。[仓库 API](https://api.github.com/repos/dotnet/aspnetcore)、[release API](https://api.github.com/repos/dotnet/aspnetcore/releases/latest)
- **采用度旁证**：`webhooks` 标签 4,945 个问题。[SO API](https://api.stackexchange.com/2.3/tags/webhooks/info?site=stackoverflow&filter=default)
- **限制**：这是服务端接收方向：生成物需要 endpoint mapping、请求验证和 payload 反序列化，不是现有客户端 `For<T>(source)` 的同构扩展。GitHub webhook 的签名验证和投递模型也说明该边界会引入验证/重试语义。[GitHub Webhooks 文档](https://docs.github.com/en/webhooks)、[ASP.NET Core routing 文档](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing)

### 3.7 FileSystemWatcher

- **包与规模**：`System.IO.FileSystem.Watcher` 4.3.0，累计 344,628,116；这是 Microsoft BCL 包，不能把总量视为“使用了 Observables 风格文件流”的用户数。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.IO.FileSystem.Watcher&prerelease=false&semVerLevel=2.0)
- **官方维护**：其实现属于 `dotnet/runtime`，该仓库 18,115 stars、5,528 forks，最近 push 2026-07-27，latest release v10.0.10 于 2026-07-15 发布。[仓库 API](https://api.github.com/repos/dotnet/runtime)、[release API](https://api.github.com/repos/dotnet/runtime/releases/latest)
- **采用度旁证**：`filesystemwatcher` 标签 1,239 个问题。[SO API](https://api.stackexchange.com/2.3/tags/filesystemwatcher/info?site=stackoverflow&filter=default)
- **限制**：官方 API 已直接暴露 `Changed`、`Created`、`Deleted`、`Renamed` event；新域的增量只能来自声明式过滤、生命周期和去抖等策略，否则与现有 Events 域重叠。[FileSystemWatcher API](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)

### 3.8 SerialPort

- **包与规模**：`System.IO.Ports` 10.0.10，累计 116,910,143，MIT。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.IO.Ports&prerelease=false&semVerLevel=2.0)
- **官方维护**：实现属于 `dotnet/runtime`；该仓库最近 push 2026-07-27，v10.0.10 发布于 2026-07-15。[仓库 API](https://api.github.com/repos/dotnet/runtime)、[release API](https://api.github.com/repos/dotnet/runtime/releases/latest)
- **采用度旁证**：`serial-port` 标签 11,694 个问题。[SO API](https://api.stackexchange.com/2.3/tags/serial-port/info?site=stackoverflow&filter=default)
- **限制**：官方 `DataReceived` event 的语义是接收缓冲区有数据，不是完整消息；生成器若不要求分帧就只能暴露字节片段。测试通常需要串口对、虚拟驱动或真实设备。[SerialPort API](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport)

### 3.9 MIDI

- **包与规模**：`NAudio` 2.3.0 累计 12,594,017；`Melanchall.DryWetMidi` 8.0.3 累计 254,941；均为 MIT。[NAudio NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ANAudio&prerelease=false&semVerLevel=2.0)、[DryWetMIDI NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3AMelanchall.DryWetMidi&prerelease=false&semVerLevel=2.0)
- **官方维护**：NAudio 仓库 6,188/1,130，最近 push 2026-07-25，v2.3.0 发布 2026-03-12；DryWetMIDI 仓库 691/83，最近 push 2026-07-24，v8.0.3 发布 2025-12-16。[NAudio API](https://api.github.com/repos/naudio/NAudio)、[NAudio release](https://api.github.com/repos/naudio/NAudio/releases/latest)、[DryWetMIDI API](https://api.github.com/repos/melanchall/drywetmidi)、[DryWetMIDI release](https://api.github.com/repos/melanchall/drywetmidi/releases/latest)
- **采用度旁证**：`midi` 标签 2,005 个问题。[SO API](https://api.stackexchange.com/2.3/tags/midi/info?site=stackoverflow&filter=default)
- **限制**：NAudio 是完整音频/MIDI 库，不能把全部下载归因于 MIDI；DryWetMIDI 更专门但累计量较小。输入设备事件天然适合流，但跨平台设备枚举和 CI 虚拟设备是独立成本。[NAudio README](https://github.com/naudio/NAudio)、[DryWetMIDI README](https://github.com/melanchall/drywetmidi)

### 3.10 WMI 事件

- **包与规模**：`System.Management` 10.0.10，累计 549,131,032，MIT；包描述明确包含 management events。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Management&prerelease=false&semVerLevel=2.0)
- **官方维护**：实现属于 `dotnet/runtime`，最近 push 2026-07-27；v10.0.10 发布 2026-07-15。[仓库 API](https://api.github.com/repos/dotnet/runtime)、[release API](https://api.github.com/repos/dotnet/runtime/releases/latest)
- **采用度旁证**：`wmi` 标签 4,374 个问题。[SO API](https://api.stackexchange.com/2.3/tags/wmi/info?site=stackoverflow&filter=default)
- **限制**：`ManagementEventWatcher` 通过 WQL 查询订阅，声明式参数非常适合特性；但 `System.Management` 的目标能力是 Windows WMI，不能按跨平台 BCL 候选对待。[ManagementEventWatcher API](https://learn.microsoft.com/en-us/dotnet/api/system.management.managementeventwatcher)

### 3.11 Windows 事件日志

- **包与规模**：`System.Diagnostics.EventLog` 10.0.10，累计 2,418,967,561，MIT。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Diagnostics.EventLog&prerelease=false&semVerLevel=2.0)
- **官方维护**：实现属于 `dotnet/runtime`，最近 push 2026-07-27；v10.0.10 发布 2026-07-15。[仓库 API](https://api.github.com/repos/dotnet/runtime)、[release API](https://api.github.com/repos/dotnet/runtime/releases/latest)
- **采用度旁证**：`event-log` 标签 1,277 个问题。[SO API](https://api.stackexchange.com/2.3/tags/event-log/info?site=stackoverflow&filter=default)
- **限制**：官方 `EventLogWatcher` 支持 XPath 查询和 `EventBookmark` 起始位置；bookmark 会引入恢复语义，事件源写入还可能要求权限。整个 API 是 Windows event log 边界。[EventLogWatcher API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventing.reader.eventlogwatcher)

### 3.12 .NET 诊断源

- **包与规模**：`System.Diagnostics.DiagnosticSource` 10.0.10，累计 9,777,937,683，MIT；这是本表最大的 NuGet 总量，但包含大量 .NET/ASP.NET/遥测传递依赖。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Diagnostics.DiagnosticSource&prerelease=false&semVerLevel=2.0)
- **官方维护**：实现属于 `dotnet/runtime`，18,115 stars、5,528 forks，最近 push 2026-07-27；v10.0.10 发布 2026-07-15。[仓库 API](https://api.github.com/repos/dotnet/runtime)、[release API](https://api.github.com/repos/dotnet/runtime/releases/latest)
- **采用度旁证**：`eventsource` 标签 2,110；`DiagnosticListener` 没有同名 SO 标签，`activity` 标签包含多个语义，未合并为一个 .NET 数字。[SO API](https://api.stackexchange.com/2.3/tags/eventsource;diagnosticlistener;activity/info?site=stackoverflow&filter=default)
- **限制与机会**：`DiagnosticListener` 官方类型本身实现 `IObservable<KeyValuePair<string, object>>`，因此形态与 Observables 对齐；新增价值更可能是强类型 payload 和统一过滤，但也最容易与现有 .NET telemetry abstraction 重叠。[DiagnosticListener API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.diagnosticlistener)、[EventSource API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventsource)、[Meter API](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter)

### 3.13 BLE / HID

- **包与规模**：`InTheHand.BluetoothLE` 4.0.44 累计 180,142，MIT；`HidSharp` 2.6.4 累计 1,490,685，NuGet 页面未给 SPDX license expression，保留为包内 license。[BluetoothLE NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3AInTheHand.BluetoothLE&prerelease=false&semVerLevel=2.0)、[HidSharp NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3AHidSharp&prerelease=false&semVerLevel=2.0)
- **官方维护**：32feet 982/225，最近 push 2026-07-20，但 latest release API 返回 4.0（2017-10-11）；HidSharp fork 206/48，最近 push 2026-07-02，没有 GitHub release。[32feet API](https://api.github.com/repos/inthehand/32feet)、[32feet release](https://api.github.com/repos/inthehand/32feet/releases/latest)、[HidSharp API](https://api.github.com/repos/IntergatedCircuits/HidSharp)
- **采用度旁证**：`bluetooth` 标签 17,326 个问题，但它覆盖移动端、Windows 和硬件问题，不是 .NET BLE/HID 子集。[SO API](https://api.stackexchange.com/2.3/tags/bluetooth/info?site=stackoverflow&filter=default)
- **限制**：BLE characteristic notification/HID input report 适合流，但需要设备、驱动和平台权限；包总下载量不能替代真实设备用户量。[32feet README](https://github.com/inthehand/32feet)、[HidSharp 项目](https://github.com/IntergatedCircuits/HidSharp)

### 3.14 命名管道 / Unix domain socket

- **包与规模**：`System.IO.Pipes` 4.3.0 累计 172,784,595；`System.Net.Sockets` 4.3.0 累计 2,356,850,399；两者都是通用 BCL 依赖，不能从总量拆出本候选。[Pipes NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.IO.Pipes&prerelease=false&semVerLevel=2.0)、[Sockets NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Net.Sockets&prerelease=false&semVerLevel=2.0)
- **官方维护**：两者都属于 `dotnet/runtime`，最近 push 2026-07-27；v10.0.10 发布 2026-07-15。[仓库 API](https://api.github.com/repos/dotnet/runtime)、[release API](https://api.github.com/repos/dotnet/runtime/releases/latest)
- **采用度旁证**：`named-pipes` 1,895；`unix-socket` 730。两个标签都不是 .NET 专属。[SO API](https://api.stackexchange.com/2.3/tags/named-pipes;unix-socket/info?site=stackoverflow&filter=default)
- **限制**：Named pipe/UDS 的底层 API 是字节流或连接流，消息边界和 framing 必须由域定义；Windows named pipe 与 Unix socket 的能力、权限和地址模型也不完全相同。[NamedPipeServerStream API](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream)、[UnixDomainSocketEndPoint API](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.unixdomainsocketendpoint)

### 3.15 D-Bus

- **包与规模**：`Tmds.DBus` 0.94.2，累计 5,037,742，MIT。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ATmds.DBus&prerelease=false&semVerLevel=2.0)
- **官方维护**：仓库 351 stars、58 forks，最近 push 2026-07-11；0.94.2 于 2026-06-17 发布。[仓库 API](https://api.github.com/repos/tmds/Tmds.DBus)、[release API](https://api.github.com/repos/tmds/Tmds.DBus/releases/latest)
- **采用度旁证**：`dbus` 标签 1,466 个问题。[SO API](https://api.stackexchange.com/2.3/tags/dbus/info?site=stackoverflow&filter=default)
- **限制**：D-Bus signal 是天然事件流，但运行环境主要是 Linux session/system bus；Tmds.DBus 已有 `dotnet dbus` tool 和 generator，从 introspection XML 生成接口，和本项目的生成器定位重叠。[Tmds.DBus generator 文档](https://github.com/tmds/Tmds.DBus/blob/main/docs/tool.md)、[D-Bus 官方规范](https://dbus.freedesktop.org/doc/dbus-specification.html)

### 3.16 OPC UA

- **包与规模**：`OPCFoundation.NetStandard.Opc.Ua` 1.5.378.156，累计 5,361,074；官方仓库声明 OPC Foundation MIT License 1.00。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3AOPCFoundation.NetStandard.Opc.Ua&prerelease=false&semVerLevel=2.0)、[LICENSE](https://github.com/OPCFoundation/UA-.NETStandard/blob/master/LICENSE.txt)
- **官方维护**：OPC Foundation 仓库 2,358 stars、1,054 forks，最近 push 2026-07-27；1.5.378.156 于 2026-07-10 发布，并包含多个修复。[仓库 API](https://api.github.com/repos/OPCFoundation/UA-.NETStandard)、[release API](https://api.github.com/repos/OPCFoundation/UA-.NETStandard/releases/latest)
- **采用度旁证**：`opc-ua` 标签 1,056 个问题；标签覆盖所有语言和工具。[SO API](https://api.stackexchange.com/2.3/tags/opc-ua/info?site=stackoverflow&filter=default)
- **限制与成熟度**：OPC UA 是成熟的工业协议生态，官方 .NET 栈同时提供 client/server；但证书、安全策略、session 与 subscription 参数会使实现复杂度高于普通事件代理。该事实支持“专业生态、维护活跃”，不等于已经决定产品优先级。[官方仓库 README](https://github.com/OPCFoundation/UA-.NETStandard/blob/master/README.md)、[OPC Foundation](https://opcfoundation.org/about/opc-technologies/opc-ua/)

### 3.17 CoAP Observe

- **包与规模**：`CoAPnet` 1.2.0，累计 75,888，MIT。[NuGet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ACoAPnet&prerelease=false&semVerLevel=2.0)
- **官方维护**：仓库 74 stars、18 forks；latest release v1.2.0 于 2022-04-30，最近 push 2022-10-16。[仓库 API](https://api.github.com/repos/chkr1011/CoAPnet)、[release API](https://api.github.com/repos/chkr1011/CoAPnet/releases/latest)
- **采用度旁证**：`coap` 标签 233 个问题。[SO API](https://api.stackexchange.com/2.3/tags/coap/info?site=stackoverflow&filter=default)
- **限制与成熟度**：CoAP Observe 的协议语义由 RFC 7641 定义，形态与“观察资源”高度匹配；但 .NET 包和 GitHub 维护信号都小，不能因为规范成熟就推断客户端生态成熟。[RFC 7641](https://www.rfc-editor.org/rfc/rfc7641)、[CoAPnet README](https://github.com/chkr1011/CoAPnet/blob/master/README.md)

## 4. 数据缺失、不可比性与限制

1. **下载量不是安装量，也不是边界使用量**：NuGet `totalDownloads` 会包含 CI、缓存失效、传递依赖、多个目标框架和重复下载；BCL 包尤其不能与专用协议包按同一含义比较。[NuGet API 文档](https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource)
2. **没有 12 个月公开时间序列**：本次官方 NuGet 查询返回累计总量与版本累计量，没有按月数据；因此 17 个候选的近 12 个月趋势统一写“不可得”，没有用第三方趋势站或搜索结果补数。
3. **SO 标签不是候选边界计数**：`postgresql`、`redis`、`bluetooth`、`activity` 等标签覆盖的主题远超 .NET 客户端；有些标签有 synonym，有些候选没有专用标签。`DiagnosticListener` 没有独立标签，故只报告 `eventsource` 并注明缺失。
4. **GitHub stars/forks 有仓库粒度偏差**：一个仓库可能包含 server、client、tool、多个包或多个协议层；`.NET runtime` 的 18,115 stars 被多个 BCL 候选复用，只能证明平台维护度，不能拆成 WMI/EventLog/Pipes 各自的采用度。
5. **release 不是完整活跃度**：GitHub API 的 `releases/latest` 只返回稳定 release 选择结果；没有 release 的仓库不应被自动判定为停止维护，反之近期 release 也不证明 API 适合本产品。本文同时保留 `pushed_at`，但未抓取完整 commit 频率序列。
6. **许可证字段存在语义差异**：NetMQ 的 LGPLv3、OPC Foundation MIT License、Tmds.DBus/StackExchange.Redis 的仓库许可证字段和 NuGet 页面表达方式不同；最终分发前仍应检查具体 `.nupkg` 内 license 与依赖树。
7. **采用度和产品适配是正交轴**：Webhook 的生态很大但它是服务端 endpoint 方向；GraphQL 有活跃生态但官方 codegen 重叠；FileSystemWatcher/EventLog/DiagnosticSource 下载量高但已有 .NET event/observable 直接 API；CoAPnet/AMQPNetLite/OPC UA 形态契合但规模不同。本文只提供证据，不把“下载量最高”转换成最终 top-N。
8. **明确排除项**：MongoDB Change Streams 不在本研究范围，因为候选普查已把它判为“部分”形态；本文没有为其收集采用度，也不应把它与这 17 个候选放入同一排名。

## 5. 一手来源索引

- NuGet 官方搜索 API：[AMQPNetLite](https://azuresearch-usnc.nuget.org/query?q=packageid%3AAMQPNetLite&prerelease=false&semVerLevel=2.0)、[StackExchange.Redis](https://azuresearch-usnc.nuget.org/query?q=packageid%3AStackExchange.Redis&prerelease=false&semVerLevel=2.0)、[NetMQ](https://azuresearch-usnc.nuget.org/query?q=packageid%3ANetMQ&prerelease=false&semVerLevel=2.0)、[Npgsql](https://azuresearch-usnc.nuget.org/query?q=packageid%3ANpgsql&prerelease=false&semVerLevel=2.0)、[HotChocolate.AspNetCore](https://azuresearch-usnc.nuget.org/query?q=packageid%3AHotChocolate.AspNetCore&prerelease=false&semVerLevel=2.0)、[StrawberryShake.Transport.WebSockets](https://azuresearch-usnc.nuget.org/query?q=packageid%3AStrawberryShake.Transport.WebSockets&prerelease=false&semVerLevel=2.0)
- NuGet 官方搜索 API：[System.IO.FileSystem.Watcher](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.IO.FileSystem.Watcher&prerelease=false&semVerLevel=2.0)、[System.IO.Ports](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.IO.Ports&prerelease=false&semVerLevel=2.0)、[NAudio](https://azuresearch-usnc.nuget.org/query?q=packageid%3ANAudio&prerelease=false&semVerLevel=2.0)、[Melanchall.DryWetMidi](https://azuresearch-usnc.nuget.org/query?q=packageid%3AMelanchall.DryWetMidi&prerelease=false&semVerLevel=2.0)、[System.Management](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Management&prerelease=false&semVerLevel=2.0)、[System.Diagnostics.EventLog](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Diagnostics.EventLog&prerelease=false&semVerLevel=2.0)
- NuGet 官方搜索 API：[System.Diagnostics.DiagnosticSource](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Diagnostics.DiagnosticSource&prerelease=false&semVerLevel=2.0)、[InTheHand.BluetoothLE](https://azuresearch-usnc.nuget.org/query?q=packageid%3AInTheHand.BluetoothLE&prerelease=false&semVerLevel=2.0)、[HidSharp](https://azuresearch-usnc.nuget.org/query?q=packageid%3AHidSharp&prerelease=false&semVerLevel=2.0)、[System.IO.Pipes](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.IO.Pipes&prerelease=false&semVerLevel=2.0)、[System.Net.Sockets](https://azuresearch-usnc.nuget.org/query?q=packageid%3ASystem.Net.Sockets&prerelease=false&semVerLevel=2.0)、[Tmds.DBus](https://azuresearch-usnc.nuget.org/query?q=packageid%3ATmds.DBus&prerelease=false&semVerLevel=2.0)、[OPCFoundation.NetStandard.Opc.Ua](https://azuresearch-usnc.nuget.org/query?q=packageid%3AOPCFoundation.NetStandard.Opc.Ua&prerelease=false&semVerLevel=2.0)、[CoAPnet](https://azuresearch-usnc.nuget.org/query?q=packageid%3ACoAPnet&prerelease=false&semVerLevel=2.0)
- GitHub 官方 API：[amqpnetlite](https://api.github.com/repos/Azure/amqpnetlite)、[StackExchange.Redis](https://api.github.com/repos/StackExchange/StackExchange.Redis)、[NetMQ](https://api.github.com/repos/zeromq/netmq)、[Npgsql](https://api.github.com/repos/npgsql/npgsql)、[ChilliCream/graphql-platform](https://api.github.com/repos/ChilliCream/graphql-platform)、[dotnet/aspnetcore](https://api.github.com/repos/dotnet/aspnetcore)、[dotnet/runtime](https://api.github.com/repos/dotnet/runtime)、[NAudio](https://api.github.com/repos/naudio/NAudio)、[DryWetMIDI](https://api.github.com/repos/melanchall/drywetmidi)、[32feet](https://api.github.com/repos/inthehand/32feet)、[HidSharp](https://api.github.com/repos/IntergatedCircuits/HidSharp)、[Tmds.DBus](https://api.github.com/repos/tmds/Tmds.DBus)、[UA-.NETStandard](https://api.github.com/repos/OPCFoundation/UA-.NETStandard)、[CoAPnet](https://api.github.com/repos/chkr1011/CoAPnet)
- Stack Exchange 官方 API：[标签信息接口](https://api.stackexchange.com/2.3/tags/amqp;redis;zeromq;postgresql;graphql;webhooks;filesystemwatcher;serial-port;midi;wmi;event-log;eventsource;diagnosticlistener;activity;bluetooth;named-pipes;unix-domain-sockets;dbus;opc-ua;coap/info?site=stackoverflow&filter=default)
- 本地范围来源：[`docs/research/io-boundary-candidates.md`](io-boundary-candidates.md)；仅用于候选清单、既有形态/E2E 背景与许可证初始线索，不替代上面的官方采用度数据源。
