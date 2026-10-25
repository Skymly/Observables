# 调研：与 ReactiveUI 重叠的 Events / RestAPI 域——继续、收缩还是转向贡献

> 取数日期：**2026-10-24**。
> 上游快照：refit [`92ae878`](https://github.com/reactiveui/refit/commit/92ae878250798dc103a01304704a99bb8033ed77)（2026-10-18），Primitives [`18e8a85`](https://github.com/reactiveui/Primitives/commit/18e8a8524dfae7f1f043bdbd3d814c5e187024c0)（2026-10-04）。只读克隆在 `C:\Code\Skymly\ref\reactiveui-refit` 与 `C:\Code\Skymly\ref\reactiveui-primitives`，不入库。逐条带行号的上游源码清单在 `C:\Code\Skymly\ref\_notes\upstream-inventory.md`。
> Fusillade、Primitives 包族、Refit 16 产品面等背景事实见 [`reactiveui-refit-fusillade-primitives.md`](reactiveui-refit-fusillade-primitives.md)，本文不重复。本文补充 Events / RestAPI 的逐项对比、需求数据和上游协作数据，并回答四个决策问题。
> 本文是调研和建议。它不改 ADR，也不改路线图。

## 0. 结论

| 问题 | 结论 |
|---|---|
| 1. 价值与差异化 | 差异化有两块：8 个与 ReactiveUI 不重叠的 IO 域，以及把 R3 当一等后端。两个重叠域里，Events 在功能上确实领先（路由事件、附加路由事件、保留 sender 的出口、泛型约束接收者、原生 R3）。RestAPI 只剩「原生 `Observable<T>` 返回」一项，而 R3 用户在 Refit 的 `IObservable<T>` 后面接一次 `ToObservable()` 就能得到同样的冷流语义。整个项目目前没有外部需求信号。 |
| 2. 继续还是转向贡献 | 不整体停止，也不按现状继续，而是收缩。RestAPI 冻结功能开发并设退场条件。Events 保留，先修 CS0122。主要精力放在独有的 IO 域。对上游只做点状贡献，首选 Refit。 |
| 3. Primitives 作第三后端 | 不接入，维持 ADR-001 的 SKIP。但 ADR-001 的理由 1 已经过时，应另起 ADR 复核。ReactiveUI 用户真正需要的是 Rx 7 兼容（R2-B2），不是第三条包线。 |
| 4. 若停止，贡献什么 | 给 Refit：让 NuGet 分发的 return-type adapter 能被生成器发现，并让 adapter 在每次订阅时重建请求。给 Primitives.ObservableEvents：路由事件与附加路由事件、保留 sender 的出口、泛型约束接收者。其余部分（8 个 IO 域、共享 IO 管道、`RestApiCallScope`、RestAPI 运行时）在上游没有合适的位置，或者不值得贡献。 |

## 1. 需求与协作现实

### 1.1 Observables 自身的需求信号

- **NuGet**（owner `Skym`，搜索 API 同日快照）：20 个 `Observables.*` 包累计下载 43,143 次。其中 `Events.R3` 2,945、`Events.Reactive` 2,920、`RestAPI.R3` 2,980、`RestAPI.Reactive` 2,951。单版本 90–135 次，例如 `Events.R3` 的 0.2.0=127、0.2.1=120、0.2.2=121、0.3.0=116、0.3.1=101（0.4.0 取数时为 0）。唯一真实消费方 GitPulse 只引用 R3 包，而 R3 包与 Reactive 包的下载量几乎相同。所以这些数字是镜像和爬虫的基线流量，不代表采用。
- **GitHub**：11 stars、1 fork、0 watchers。[`ROADMAP.md`](../ROADMAP.md) 现状基线写的是「Issue 全部来自维护者；真实消费方仅 GitPulse。无外部反馈」。
- **投入**：2026-06-01 以来 582 次提交（06 月 186、07 月 65、08 月 97、09 月 219、10 月至今 15）。非测试 C# 约 3.0 万行，测试 C# 约 2.7 万行（`git ls-files` 粗略统计）。维护者只有 1 人。

### 1.2 对照：同类包的采用度

同日 NuGet 搜索 API 的累计下载：

| 包 | 累计下载 | 说明 |
|---|---:|---|
| `Refit` | 202,315,266 | 16.4.0 单版本 62,211 |
| `Refit.HttpClientFactory` | 126,490,103 | |
| `ReactiveUI.Primitives` | 3,983,331 | 9.0.0 单版本 111,002。主要来自 ReactiveUI 26 和 Refit 16 的传递依赖 |
| `ReactiveUI.Primitives.ObservableEvents` | 3,681 | 2026-08-15 才首次提交（`9681ccc8`），和 Observables.Events 一样处在基线 |
| `ReactiveUI.Primitives.R3Bridge.Generator` | 1,770 | 9.0.0 单版本 88 |
| `ReactiveMarbles.ObservableEvents.SourceGenerator` | 597,344 | 仓库已归档，最后 release 1.3.1 在 2023-10-02，只输出 System.Reactive |
| `R3Events` / `R3Extensions.ObservableEvents.SourceGenerator` / `DionTools.EventAsR3ObservableSourceGenerator` | 2,371 / 1,944 / 1,867 | 一群面向 R3 的事件生成器小包，全部在基线 |
| `R3` | 4,509,940 | Cysharp/R3 有 4,026 stars |

解读：事件转 Observable 的生成器，真实存量在已归档的 ReactiveMarbles 包上。ReactiveUI 自己的新生成器，以及包括本仓在内的所有 R3 事件生成器，都还没有用户。HTTP 客户端的存量在 Refit。

### 1.3 上游的方向：Primitives 优先，R3 是可选桥

- ReactiveUI 26.0.1 的 net8.0 依赖闭包里只有 `ReactiveUI.Core`、`ReactiveUI.Binding`、lean `ReactiveUI.Primitives` 9.0.0 等，**没有 System.Reactive**。Refit 16.4.0 在所有 TFM 上都依赖 `ReactiveUI.Primitives` 9.0.0。System.Reactive 用户改用孪生包 `ReactiveUI.Reactive` 26.0.1（58,595 次下载），它依赖 **System.Reactive 7.0.0**。
- R3 在 ReactiveUI 里是可选桥接。refit 全仓没有任何 R3 或 Cysharp 引用，自己的构建还会剥掉传递进来的 R3Bridge 分析器（[`src/Directory.Build.targets:23-31`](https://github.com/reactiveui/refit/blob/92ae878250798dc103a01304704a99bb8033ed77/src/Directory.Build.targets#L23-L31)）。Refit 12/13 的用户曾被传递进来的 R3Bridge 弄坏构建（CS0436，[refit#2176](https://github.com/reactiveui/refit/issues/2176)、[refit#2194](https://github.com/reactiveui/refit/issues/2194)）。维护者的回应是 "make the source generator for R3 optional. Not all users will benefit, and R3 users can opt in."
- 结论：ReactiveUI 以 Primitives 为中心，R3 处在边缘。Observables 以 R3 为中心。两边在后端取向上不同向。

### 1.4 版本节奏

| 仓库 | 2026 年的 major 版本 |
|---|---|
| refit | 11.0.0（06-03）→ 12.0.0（06-24）→ 13.0.0（07-01）→ 14.0.0（07-25）→ 15.0.0（08-06）→ 16.0.0（09-20）。最新 16.4.0（10-18） |
| Primitives | v1.0.0（06-01）→ v9.0.0（10-03），共 33 个 tag。v8 改了通知投递语义（`49e25158`），v9 改了 `SubscribeSafe` 语义（`56523df0`）。仓库没有 CHANGELOG，也没有 breaking-change 文档 |

### 1.5 贡献动态

数据来自 `gh pr list`，范围是 2026-04-24 以来创建的 PR。排除 `glennawatson`、`ChrisPulman` 和 bot 之后算作外部 PR：

| | refit | Primitives |
|---|---|---|
| PR 总数 | 206 | 213 |
| 外部 PR | 34 个，来自 11 位作者 | 1 个（#188，文档） |
| 外部 PR：合并 / 关闭未合并 / 开放 | 29 / 5 / 0 | 1 / 0 / 0 |
| 外部 PR 合并耗时 | 中位 0.6 天，最长 5.5 天 | 2.3 天 |
| 已合并的外部功能 PR 举例 | keyed DI 注册 #2297、`CollectionFormat.Indexed` #2262、`TransportExceptionFactory` #2205、URL 路径参数的生成 #2174 | — |
| CLA | 无。PR 检查是三平台构建、CodeQL、AOT smoke、netfx smoke、Sonar | 无。CONTRIBUTING 写明 "Major Changes ... should be discussed first in Discussions or Slack" |
| 近 6 个月提交最多的作者（本地 `git shortlog`） | Glenn Watson 112、renovate 52、Tim M 11 | Glenn Watson 77、Chris Pulman 60、renovate 55 |

refit 对外部贡献友好，而且响应快。Primitives 实际上是两位维护者自己的仓库。

## 2. Events：Observables.Events 对 Primitives.ObservableEvents

上游引用都是 `prim:` = [Primitives@18e8a85](https://github.com/reactiveui/Primitives/tree/18e8a8524dfae7f1f043bdbd3d814c5e187024c0) 下 `src/ReactiveUI.Primitives.ObservableEvents/` 的相对路径。

| 能力 | Observables.Events 0.4.0 | Primitives.ObservableEvents 9.0.0 |
|---|---|---|
| 输出后端 | R3 包输出 `Observable<T>`，Reactive 包输出 `IObservable<T>`，由所引用的包决定 | 探测 compilation 里有哪个提供者，优先级 lean Primitives > `.Reactive` > System.Reactive（`Helpers/ProviderResolver.cs:16-43`）。**不输出 R3**，R3 用户要再装 R3Bridge 并调用 `.AsR3Observable()` |
| 路由事件（WPF / Avalonia，`AddHandler` + `handledEventsToo` + `RoutingStrategies`） | 有，需开 `ObservableRoutedEvents`（[`events.md`](../design/events.md) §5.3） | **没有**。生成器里找不到 `RoutedEvent`、`AddHandler`、`handledEventsToo`，只用 CLR `+=`（`CodeGeneration/SourceFileWriter.cs:70-95`） |
| Avalonia 附加路由事件 | 有（`.AttachedRoutedEvent<T>()`） | 没有 |
| 保留 sender 的出口 | `.EventHandlers()` 输出 `(sender, e)` | 没有。`(object, T)` 委托会丢掉 sender（`Helpers/EventExtractor.cs:136-159`） |
| 泛型形参接收者（`T : INotifyPropertyChanged`） | 支持（`GenericConstraintTarget`） | 不生成（`Helpers/InstanceTargetExtractor.cs:44-52`，测试 `EventGeneratorIgnoresUnresolvableEventReceivers`） |
| 静态事件 | 关闭（[`ObservableEventsConstants.cs:43`](../../Observables.Events/Observables.Events.SourceGenerators.Shared/ObservableEvents/ObservableEventsConstants.cs) 里 `StaticObservableEventsGenerationEnabled = false`） | 支持，用 `[assembly: GenerateStaticEventObservables(typeof(Host))]` |
| 事件收集范围 | 沿 `BaseType` 和 `type.Interfaces` 递归，只检查事件本身是否 `public`（[`InterfacePipeline.cs:103-112, 184-194`](../../Observables.Events/Observables.Events.SourceGenerators.Shared/ObservableEventsGenerator.InterfacePipeline.cs)） | 只沿 `BaseType`，只收 public 事件（`Helpers/EventExtractor.cs:33-41`） |
| 引用程序集里 internal 接口导致的 CS0122 | **会触发**。GitPulse 在 MAUI `SearchBar.Events()` 上撞到了内部接口 `IControlsVisualElement`，只能改用公开事件适配器绕开（GitPulse `docs/adr/ADR-015-searchbar-public-event-adapter.md`）。主仓没有对应 issue | 因为不遍历接口，这种情形不会出现。但它同样没有可见性检查，public 事件声明在 internal 基类上时理论上仍可能出错 |
| 真实平台程序集测试 | 没有。测试用手写桩（`AvaloniaStubs`、伪造的 `System.Windows` 命名空间）。用真实 Avalonia 11.2.1 的路由事件样例 `Observables.Samples.Events.Routed` 设置了 `ObservablesExcludeFromCi=true`，最后一次改动在 2026-06-03 | 有。测试直接加载真实 WPF、WinForms、Avalonia、MAUI 程序集（`tests/.../EventGeneratorTests.Platforms.cs:61-181`） |
| 增量性 | 所有候选 `Collect()` 后与 `CompilationProvider` 合并，每次编译变更都重新解析，只有发射阶段能命中缓存（[`ObservableEventsGenerator.cs:42-91`](../../Observables.Events/Observables.Events.SourceGenerators.Shared/ObservableEventsGenerator.cs)） | 每个节点单独提取值相等的 record 模型，并有专门的增量缓存测试（`EventGenerator.cs:20-50`，`tests/.../EventGeneratorTests.Incremental.cs`） |
| 诊断 / 测试数 | OBS2001–2006；75 个 `[Fact]/[Theory]` | RXOE001–003；80 个 `[Test]` |

结论：

- **功能**上本仓领先 4 项：路由事件、附加路由事件、保留 sender 的出口、泛型约束接收者，再加上原生 R3 输出。
- **工程质量**上 Primitives 领先 4 项：不会因 internal 接口报 CS0122、用真实平台程序集测试、增量性更好、支持静态事件。
- 本仓领先的路由事件，在真实框架上只靠一个不进 CI 的 Avalonia 样例验证过，WPF 路由事件没有任何真实程序集验证。
- [`events.md`](../design/events.md) §10 写「跨程序集事件发现（当前仅限本程序集内 `event` 成员）」，与代码不符。发现逻辑走语义模型，引用程序集里的类型同样会被处理。GitPulse 的 CS0122 正是因为生成器处理了 MAUI 程序集里的 `SearchBar` 才出现的。

## 3. RestAPI：Observables.RestAPI 对 Refit 16.4.0

上游引用都是 `refit:` = [refit@92ae878](https://github.com/reactiveui/refit/tree/92ae878250798dc103a01304704a99bb8033ed77) 下 `src/` 的相对路径。

| 面 | Observables.RestAPI 0.4.0 | Refit 16.4.0 |
|---|---|---|
| R3 `Observable<T>` 返回 | 原生，每次订阅重建请求（`R3.Observable.FromAsync`） | 不支持，见下面三条路 |
| `IObservable<T>` 返回 | 由 18 行的 [`SystemReactiveObservableAdapter`](../../Observables.RestAPI/Observables.RestAPI.Reactive/SystemReactiveObservableAdapter.cs) 调用 System.Reactive 的 `Observable.FromAsync` | 由 `GeneratedRequestRunner.SendObservable` 返回 Primitives 的 `FromAsyncSignal`。冷流，每次订阅重建请求，订阅释放时取消（`Refit/GeneratedRequestRunner.Sending.cs:88-130`） |
| 其他返回类型 | `Task`、`ValueTask`、`IApiResponse<T>`、同步值、`void` | 9 种 `ReturnTypeInfo`：另有 `IAsyncEnumerable<T>`、`PagedEnumerable`、未发送的 `HttpRequestMessage`、adapter（`InterfaceStubGenerator.Shared/Models/ReturnTypeInfo.cs:7-35`） |
| 查询对象展开 | 不展开（[`restapi.md`](../design/restapi.md) §3.4） | 编译期按声明类型展开 |
| 流（JSON 数组 / JSON Lines / SSE） | 没有，SSE 是独立域 | 有（`Refit/StreamingContentFormat.cs`） |
| 分页、重试、测试包 | 没有 | `PagedEnumerable`、按响应头退避的 `HttpRetryHandler`、`Refit.Testing` |
| DI 包 | `Observables.RestAPI.HttpClientFactory` 没有发到 nuget.org | `Refit.HttpClientFactory`，累计 1.26 亿次下载 |
| 每次调用的选项 | `RestApiCallScope`（基于 `AsyncLocal` 的环境作用域，0.4.0 新增）+ `[Property]`。GitPulse 还停在 0.3.0，没有用到它 | `[Property]`、`[Timeout]`、请求 options 键 |
| 维护 | 1 人，没有外部用户 | 活跃，外部 PR 中位 0.6 天合并 |

R3 用户在 Refit 上有三条路：

1. **`IObservable<T>` 加 R3 的 `ToObservable()`**（R3 README 的 "Interoperability with `IObservable<T>`" 一节）。Refit 的冷流每次订阅都重建请求，并且按订阅取消，语义和本仓 `R3.Observable.FromAsync` 一样，只是多调用一次。
2. **`Task<T>` 加 `Observable.FromAsync(ct => api.X(ct))`**。
3. **自写 `IReturnTypeAdapter<Observable<T>, T>`**。生成器只扫描消费者自己的源码程序集（`InterfaceStubGenerator.Shared/ReturnTypeAdapterParsing.cs:27-45` 的 `compilation.Assembly.GlobalNamespace`），所以 adapter 做不成可复用的 NuGet 包。生成路径还会先把请求建好，再捕获进传给 `Adapt` 的委托，第二次调用会发送已释放的请求（`InterfaceStubGenerator.Shared/InlineRequestEmission.cs:451-501`），所以也做不成真正的冷流。

结论：RestAPI 剩下的差异只有三点：省一次 `ToObservable()`、不带 `ReactiveUI.Primitives` 传递依赖，以及一个还没有用户的 `RestApiCallScope`。Refit 有查询对象展开、流、分页、重试、测试包、JSON context 和 DI 包，下载量 2 亿。这三点撑不起一条长期追赶 Refit 的路线。

## 4. 决策

### 4.1 价值与差异化（问题 1）

真正有价值、ReactiveUI 没有的部分：

1. **8 个独有 IO 域**：SignalR、MQTT、WebSocket、gRPC、SSE、NATS、PostgreSQL LISTEN/NOTIFY、Redis Pub/Sub。它们都是「声明接口，生成反应式代理」的统一模型。NuGet 上没有别的 R3 包覆盖这些边界。Rx 时代的同类包是逐协议手写的封装，例如 `MQTTnet.Extensions.External.RxMQTT.Client`（111,834）、Chris Pulman 个人的 `MQTTnet.Rx.Client`（69,244）、`RxSignalrStreams.Extensions`（74,752），都不是生成器。
2. **R3 一等后端，且与 System.Reactive 严格隔离**：域运行时后端中立（ADR-003），PackVerify 按程序集引用表拦截跨后端依赖，OBS0001 拦截同一域同时引用两个包。ReactiveUI 的做法正好相反：lean 与 `.Reactive` 用 MSBuild 别名做孪生包，R3 走桥接生成器。
3. **工程纪律**：82 个已发布诊断、约 1550 个测试、PublicAPI 基线、nuget-smoke 消费者、双 OS CI。这是作品集和后续给上游贡献时的信用。用户不会因为这一点选用它。

在两个重叠域上：Events 的领先是真实的（见 §2），但被 CS0122 和缺少真实平台测试削弱。RestAPI 基本没有差异（见 §3）。

### 4.2 继续投入，还是停下来给 ReactiveUI 做贡献（问题 2）

**建议：继续，但收缩。不要停下来整体转向 ReactiveUI。**

不建议整体停止的原因：

- 大部分价值在上游找不到位置。8 个 IO 域不属于 refit 或 Primitives 的范围，贡献过去等于在别人的组织下另起一个项目。
- 后端取向相反。ReactiveUI 以 Primitives 为中心，R3 只是可选桥接（§1.3）。整体迁过去就要放弃 R3 优先这个定位。
- 能贡献给上游的东西本来就不多，而且都是点状改动（§4.4），不需要停掉本项目才能做。

不建议按现状继续的原因：

- 5 个月、20 个包，没有一个外部用户（§1.1）。
- RestAPI 正在和一个下载量 2 亿、一个月发一个 major 的项目比功能广度，而 R3 用户在 Refit 上已经能得到同样的语义。
- 一个人维护 20 个包加 Docs、Samples 三个仓库，成本是持续的。

收缩方案：

| 域 | 动作 | 退出或复审条件 |
|---|---|---|
| RestAPI | 立即冻结功能开发，只修 GitPulse 遇到的缺陷。不追查询对象展开、流、分页。在 README 里写明：需要这些能力时用 Refit + `ToObservable()` | 满足其一就迁移 GitPulse 到 Refit，并在 NuGet 上把 `Observables.RestAPI.*` 标为 deprecated，指向 Refit：① §4.4 第 1 项被 Refit 接受；② 你认为多一次 `ToObservable()` 可以接受 |
| Events | 保留。依次修：① CS0122（遍历接口和基类时用 `Compilation.IsSymbolAccessibleWithin` 过滤）；② 照 Primitives 的做法，在测试里加载真实 WPF、Avalonia、MAUI 程序集；③ 再决定要不要打开静态事件 | 修完后，若到 1.0.0 仍没有外部信号，转为维护模式 |
| 8 个 IO 域 | 按 ROADMAP 推进：R1 发版同步、R2 MQTTnet 5、R3-1 SSE 韧性。扩域继续暂停 | 设一个需求检查点（建议 1.0.0 之后 3 个月）。外部信号指外部 issue、PR、消费者，或单版本下载明显高于每版约 120 次的基线。到点仍为零，就整体转为维护模式 |
| 上游 | 只做 §4.4 的第 1 项（Refit），视维护者反馈再考虑第 2 项（Primitives） | — |

这个建议取决于你的目标：

- 如果主要想要**影响力和社区认可**，同样的时间投到 Refit 回报高得多：9,579 stars，外部 PR 中位 0.6 天合并，feature request 模板写明 "priority will be given to those who are Contributors"。
- 如果主要想要**一个自己掌控的产品或作品集**，Observables 值得继续，但要按上表收缩。

### 4.3 是否接入 Primitives 作为第三后端（问题 3）

**不接入。** 维持 [ADR-001](../adr/ADR-001-primitives-backend-skip.md) 的结论，但换一套理由：

| 理由 | 证据 |
|---|---|
| 没有需求 | 本仓没有外部用户。Primitives 自己的事件生成器 3,681 次、R3Bridge 1,770 次下载，都在基线。lean Primitives 的 398 万次是 ReactiveUI 26 和 Refit 16 的传递依赖 |
| 不稳定 | 4 个月从 v1 到 v9，v8、v9 都改了运行时语义，没有 CHANGELOG（§1.4）。跟进它意味着 10 个新包大约每两周就要过一次 major |
| 成本主要在矩阵，不在代码 | 现有 9 个 `.Reactive` 桥只用到 `Observable.FromAsync`、`Observable.Create`、`System.Reactive.Unit` 和一处 `Disposable.Create`，每个 18–132 行，换成 `Signal.*` 和 `RxVoid` 很便宜。贵的是 20 包变 30 包、生成器的第三条 `#if`、测试和 smoke 消费者、PackVerify 规则，以及 Docs 和 Samples |
| 已有更便宜的互通方式 | `.Reactive` 包输出的是 BCL 的 `IObservable<T>`，Primitives 的操作符可以直接用。摩擦只有两处：同一编译里 System.Reactive 和 lean Primitives 的 `Subscribe` 扩展会产生 CS0121 歧义（Primitives README 给了 3 种解法），以及 `Unit` 与 `RxVoid` 不是同一类型。使用 `ReactiveUI.Reactive` 的项目两者都可以避开，但它要求 System.Reactive 7.0.0，而本仓钉的是 6.0.1，Rx 7 兼容（ROADMAP R2-B2）还没做 |
| 上游自己也不这样做 | Primitives 的做法是一份实现，再用提供者探测和桥接生成器去适配其他后端，而不是并行维护多条包线 |

ADR-001 的理由 1「Primitives 缺少 `FromAsync` / `Create` / `FromEvent` 工厂」对 v9 已不成立：现在有 `Signal.FromAsync`、`Signal.Create`、`Signal.FromEvent` / `FromEventPattern`。按 ADR 不可变的规则，应另起一条 ADR 复核，不要改 ADR-001 正文。

建议的重新评估条件，满足任一即可：

- 有真实用户要在 ReactiveUI 26 lean（不含 System.Reactive）的项目里用 Observables；
- Primitives 连续 6 个月没有 major 版本。

真有需求时的最省路径：① 先完成 R2-B2，让 `.Reactive` 包能和 `ReactiveUI.Reactive`（System.Reactive 7）共存；② 在 Docs 里写清 CS0121 的解法；③ 仍不够时，才考虑 ADR-001 提过的轻量方案，即检测到 Primitives 就发射转换扩展的桥接生成器。

### 4.4 若停止：哪些设计或实现值得贡献给 ReactiveUI（问题 4）

按价值和被接受的可能性排序：

| # | 贡献 | 目标 | 价值 | 被接受的可能 | 本仓可复用的部分 |
|---|---|---|---|---|---|
| 1 | **让 NuGet 分发的 return-type adapter 可用**：① 发现范围扩大到引用程序集。建议用程序集级 attribute 声明 adapter，避免扫描所有引用程序集的类型；② 生成的 adapter 调用改成在委托内部构建请求，就像 `SendObservable` 那样，让 adapter 可以多次调用，从而做成真正的冷流 | refit `InterfaceStubGenerator.Shared/ReturnTypeAdapterParsing.cs`、`InlineRequestEmission.cs` | 高。它和 R3 无关，`Result<T>`、LanguageExt 这类包装类型都能受益。做成之后，R3 支持只需要一个约 20 行的 adapter 包 | 较高。refit 合并过多个外部功能 PR。adapter 是 Glenn 在 2026-07-14 的 `e511df2`（#2210，`feat!`）里加的，所以要先开 issue 说明设计 | 设计经验：反应式返回类型需要「请求工厂」，不能用捕获好的请求。代码要按 refit 的结构重写 |
| 2 | **路由事件与附加路由事件**：WPF 用 `AddHandler(routedEvent, handler, handledEventsToo)`；Avalonia 用 `AddHandler<T>(routedEvent, handler, routes, handledEventsToo)`；再加附加路由事件入口 | Primitives.ObservableEvents，或 Wpf / Avalonia 平台包里的手写工厂 | 高，面向 UI 用户。CLR `+=` 收不到已处理的路由事件 | 不确定。Primitives 历史上只有 1 个外部 PR，大改动要先在 Discussions 讨论 | 检测与发射逻辑：[`ObservableEventsGenerator.RoutedDetection.cs`](../../Observables.Events/Observables.Events.SourceGenerators.Shared/ObservableEventsGenerator.RoutedDetection.cs)、[`ObservableEventsGenerator.AttachedRouted.cs`](../../Observables.Events/Observables.Events.SourceGenerators.Shared/ObservableEventsGenerator.AttachedRouted.cs)、[`events.md`](../design/events.md) §5.3–5.4。需要按对方的逐节点 record 管道和 StringBuilder 发射重写，并补上真实 WPF / Avalonia 程序集测试（本仓目前没有） |
| 3 | **保留 sender 的出口**（对应 `.EventHandlers()`，输出 `EventPattern` 形状） | Primitives.ObservableEvents | 中 | 不确定，同上 | 设计和测试用例 |
| 4 | **泛型约束接收者**（`T : INotifyPropertyChanged` 上的 `.Events()`） | Primitives.ObservableEvents | 中低 | 不确定 | `ObservableEventsGenerator.GenericConstraints.cs` 的思路 |
| 5 | CS0121 `Subscribe` 歧义的分析器和代码修复（提示改用 `SubscribePrimitives` 或 extern alias） | Primitives | 中低。README 已经给了手工解法 | 不确定 | 写分析器和 CodeFix 的经验。OBS0001 只认本仓自己的包，不能直接搬 |

不值得贡献，或者在上游没有位置的：

- **8 个 IO 域**：refit 和 Primitives 都不收。搬到 ReactiveUI 组织下等于新开项目。
- **共享 IO 生成器管道**（[`IoProxyGeneratorPipeline.cs:30-57`](../../Observables.Shared/Observables.SourceGenerators.Shared/IoProxyGeneratorPipeline.cs)）：同样是 `Collect()` 后合并 `CompilationProvider`，增量性不如上游。
- **`RestApiCallScope`**：环境作用域和 refit 显式参数（`[Property]`、`[Timeout]`）的风格冲突，而且本仓自己也还没有用户。
- **RestAPI 运行时和生成器**：Refit 在几乎每个维度上都领先。
- **按接口递归收集事件**：这正是 CS0122 的来源，Primitives 只走 `BaseType` 更稳妥。
- **PackVerify 后端隔离规则**：上游用的是别名孪生包，这套规则不适用。

真要停止的话，收尾就是三件事：在 NuGet 上把各包标为 deprecated，并指向替代品（RestAPI 指向 Refit；Events 指向 Primitives.ObservableEvents + R3Bridge）；把仓库设为只读归档；GitPulse 迁移到 Refit。

## 5. 顺带发现、应单独处理的问题

1. **Events CS0122（缺陷）**：唯一的真实消费方已经撞到，主仓没有 issue。修法：遍历接口和基类时用 `Compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly)` 过滤，并用真实 MAUI 程序集补一条回归测试。
2. **NOTICE 许可证写错**：[`NOTICE.md`](../../NOTICE.md) 和 [`restapi.md`](../design/restapi.md) 第 17 行说 Refit 是 Apache-2.0。实际上 Refit 的 LICENSE 全部历史都是 MIT：
   - 2026-06-19 之前最后一个提交 [`3717256`](https://github.com/reactiveui/refit/blob/3717256d32f6dac0503e66aad68795fe1929a2f7/LICENSE) 是 "The MIT License (MIT) Copyright (c) ReactiveUI 2012 - 2025"；
   - 2023 年的 [`b5ce1db`](https://github.com/reactiveui/refit/blob/b5ce1dbd870eae8764e308580e3d9939dc4a3b1e/LICENSE) 版本是 MIT，版权行是 ".NET Foundation and Contributors"；
   - `COPYING` 是 MIT，"Copyright (c) 2012 GitHub"。

   NOTICE 应改为 MIT，并写上对应的版权行。[`reactiveui-refit-fusillade-primitives.md`](reactiveui-refit-fusillade-primitives.md) §9 第一条未决项可以据此结案。
3. **[`events.md`](../design/events.md) §10「跨程序集事件发现」那条已经过时**（见 §2）。
4. **ADR-001 理由 1 过时**（见 §4.3）。
5. **路由事件缺少真实框架验证**：唯一的真实 Avalonia 样例不进 CI，WPF 路由事件只有桩测试。

## 6. 局限

- 下载量是累计值，不是活跃安装数。小包的基线噪声没法精确扣除，本文只用「R3 包和 Reactive 包下载量几乎相同」来判断它是噪声。
- 「外部 PR」按作者 login 排除名单判断。Chris Pulman 算作内部。Tim M 等多次贡献的人仍算外部。
- §4.4 第 1 项的改法是根据 refit 生成代码的结构推断的，没有动手构建验证，也还没问过维护者的意见。路由事件是否会被 Primitives 接受，同样没有问过。
- 没有评估 8 个独有 IO 域各自的潜在需求。这部分见 [`ecosystem-adoption.md`](ecosystem-adoption.md) 与 ADR-002。
