# Refit、Fusillade、ReactiveUI.Primitives 与 Observables

> 取数日期：**2026-10-24**。
> 范围：三个上游仓库的默认分支源码、已发布 NuGet 包，以及本仓 RestAPI / Events / ADR-001。本文不改 ADR，也不做采用决策之外的实现建议落地。

## 1. 取数口径

事实只取一手来源。二手博客不计入。

| 来源 | 用法 | 限制 |
|---|---|---|
| 浅克隆默认分支 | 读 README、LICENSE、csproj、关键类型 | `pushed_at` 不是默认分支 HEAD 日期。克隆在取数后即弃，正文用 commit SHA 的 GitHub blob 链接 |
| GitHub REST `repos` / `releases/latest` | stars、forks、`open_issues_count`、release 名 | 未认证请求随后触发 rate limit，没能补 ISO 时间戳。日期按本机 Asia/Singapore 的 `d/M/yyyy` 解读。`open_issues_count` 含 PR，不是纯 issue 数。Refit 的 `license.spdx_id` 是 `NOASSERTION`，以仓库 `LICENSE` 正文为准 |
| NuGet 搜索 API `totalDownloads` 与版本 `downloads` | 累计下载、近期版本下载 | 累计下载不是活跃安装数，也不是 12 个月趋势。版本 `downloads` 是该版本累计，不是日增量 |
| NuGet flat container nuspec | 已发布包的依赖与 `licenseUrl` | nuspec 的 `license` 元素在本次解析里是 XML 节点，许可证结论用 `licenseUrl` 加仓库 `LICENSE` |
| 本仓源码与 ADR | 对比基线 | 本仓版本以 `eng/Observables.Package.props` 的 `0.4.0` 为准 |

克隆 HEAD：

| 仓库 | SHA | HEAD 提交时间与说明 |
|---|---|---|
| [refit](https://github.com/reactiveui/refit) | [`92ae878250798dc103a01304704a99bb8033ed77`](https://github.com/reactiveui/refit/commit/92ae878250798dc103a01304704a99bb8033ed77) | 2026-10-18，`feat(http): add header-aware retries (#2364)` |
| [Fusillade](https://github.com/reactiveui/Fusillade) | [`843006e90e49f2e0a6897ee838d1d6bcce9a0628`](https://github.com/reactiveui/Fusillade/commit/843006e90e49f2e0a6897ee838d1d6bcce9a0628) | 2026-10-04，`chore(deps): update dependency tunit to 1.72.10 (#586)` |
| [Primitives](https://github.com/reactiveui/Primitives) | [`18e8a8524dfae7f1f043bdbd3d814c5e187024c0`](https://github.com/reactiveui/Primitives/commit/18e8a8524dfae7f1f043bdbd3d814c5e187024c0) | 2026-10-04，`chore(deps): update dependency tunit to 1.72.10 (#232)` |

已发布包与默认分支不是同一快照。Fusillade 7.0.0 的 nuspec 依赖 `ReactiveUI.Primitives` 7.1.0；默认分支 props 已钉到 8.2.0。下文凡写「已发布」都指 nuspec，「源码」都指上表 SHA。

## 2. 总览

| | Refit | Fusillade | Primitives | Observables |
|---|---|---|---|---|
| 解决的问题 | 把 REST 接口变成 `HttpClient` 实现 | 用 `HttpMessageHandler` 做去重、优先级限流、投机预取、可选缓存 | 不依赖 System.Reactive / R3 的 `IObservable<T>` 原语 | 把多种 IO / 事件边界生成到 R3 或 System.Reactive |
| 机制 | 源生成器为主，反射构建器拆到可选包 | handler 组合，不生成客户端 | 运行时库 + 事件生成器 + R3 桥接生成器 | 每域双路源生成器；域运行时后端中立 |
| 反应式底座 | 已发布 16.4.0 依赖 Primitives 9.0.0；`IObservable<T>` 由 `FromAsyncSignal` 实现 | 主包依赖 Primitives；另有 `fusillade.reactive` | 自己就是底座。`.Reactive` 包再依赖 System.Reactive 7 | R3 1.3.0 与 System.Reactive 6.0.1，两个消费者包互斥 |
| 许可证（当前上游） | 仓库 `LICENSE` 为 MIT | MIT | MIT | 本仓 NOTICE 仍把适配代码写成 Apache-2.0 |
| 采用度 | 高，且是直接依赖 | 累计中等，当前 major 几乎没人下 | GitHub 极低；NuGet 下载主要是传递依赖 | 个人项目，nuget.org `0.4.0`，20 个包 |

GitHub API（2026-10-24，[`refit`](https://api.github.com/repos/reactiveui/refit)、[`Fusillade`](https://api.github.com/repos/reactiveui/Fusillade)、[`Primitives`](https://api.github.com/repos/reactiveui/Primitives)）：

| 仓库 | stars / forks | `open_issues_count` | 创建 | `releases/latest` | API `pushed_at`（本地日） |
|---|---:|---:|---|---|---|
| refit | 9579 / 781 | 3 | 2013-07-22 | [16.4.0](https://github.com/reactiveui/refit/releases/tag/v16.4.0)，2026-10-17 | 2026-10-23 |
| Fusillade | 312 / 30 | 12 | 2014-04-24 | [7.0.0](https://github.com/reactiveui/Fusillade/releases/tag/v7.0.0)，2026-08-10 | 2026-10-23 |
| Primitives | 15 / 4 | 13 | 2026-05-25 | [9.0.0](https://github.com/reactiveui/Primitives/releases/tag/v9.0.0)，2026-10-03 | 2026-10-23 |

NuGet 搜索 API 累计下载（同一天）：[Refit](https://azuresearch-usnc.nuget.org/query?q=packageid%3ARefit&prerelease=false&semVerLevel=2.0) 202,315,266；[Refit.HttpClientFactory](https://azuresearch-usnc.nuget.org/query?q=packageid%3ARefit.HttpClientFactory&prerelease=false&semVerLevel=2.0) 126,490,103；[Refit.Newtonsoft.Json](https://azuresearch-usnc.nuget.org/query?q=packageid%3ARefit.Newtonsoft.Json&prerelease=false&semVerLevel=2.0) 45,618,664；[fusillade](https://azuresearch-usnc.nuget.org/query?q=packageid%3Afusillade&prerelease=false&semVerLevel=2.0) 497,123；[ReactiveUI.Primitives](https://azuresearch-usnc.nuget.org/query?q=packageid%3AReactiveUI.Primitives&prerelease=false&semVerLevel=2.0) 3,983,331；[ReactiveUI](https://azuresearch-usnc.nuget.org/query?q=packageid%3AReactiveUI&prerelease=false&semVerLevel=2.0) 20,570,881。

近期版本下载，用来把「累计」和「当前 major」分开：Refit 16.4.0 为 62,211；Primitives 9.0.0 为 111,002，8.2.0 为 416,216；fusillade 7.0.0 只有 406。Primitives 仓库 2026-05-25 才建立，398 万累计下载不能当成这个仓库的独立用户数。

## 3. Refit

### 3.1 定位

Refit 把带 attribute 的 C# 接口变成 HTTP 客户端。应用保留 `HttpClient`、取消和序列化。[README](https://github.com/reactiveui/refit/blob/92ae878250798dc103a01304704a99bb8033ed77/README.md) 写明源生成器在编译期生成实现。设计来自 Square Retrofit，但 LICENSE 写明不含 Retrofit 源码。

已发布包（README + nuspec）：

| 包 | 角色 | 已核实依赖 |
|---|---|---|
| `Refit` 16.4.0 | 生成客户端、attribute、序列化、响应类型；包内带生成器、分析器、code fix | [nuspec](https://api.nuget.org/v3-flatcontainer/refit/16.4.0/refit.nuspec)：`ReactiveUI.Primitives` 9.0.0。net8/net9 另有 `System.Net.ServerSentEvents` 10.0.13。`licenseUrl` 为 MIT |
| `Refit.HttpClientFactory` | `IHttpClientFactory` / DI | 同版本，依赖 `Refit` 与 `Microsoft.Extensions.Http` |
| `Refit.Testing` | 本地回复、请求匹配、故障注入 | nuspec 依赖 `Refit` 16.4.0 |
| `Refit.Newtonsoft.Json` / `Refit.Xml` | 可选序列化器 | Xml 16.4.0 nuspec 已核实依赖 `Refit` |
| `Refit.Reflection` | 可选运行时反射构建器 | 源码项目存在。本次没单独打开它的 nuspec，不写它的依赖表 |

源码目标框架按 README：.NET 8/9/10/11 与 .NET Framework 4.6.2–4.8.1。生成器要求 Roslyn 4.8+，包里只放一个 `analyzers/dotnet/roslyn4.8/cs` 槽，避免非 SDK 工程把多槽分析器全部喂给编译器。见 [`Refit.csproj`](https://github.com/reactiveui/refit/blob/92ae878250798dc103a01304704a99bb8033ed77/src/Refit/Refit.csproj)。

### 3.2 生成路径，不再是「反射库」

这是和 Observables 2026-06 适配时的 Refit 最重要的分叉。

- 主包生成内联请求。反射构建器移到 `Refit.Reflection`。README 与 `docs/breaking-changes.md` 写明：接口方法不能内联时诊断 RF006；没装反射包时，`RestService.For` / `AddRefitClient` 对需要反射的方法抛 `NotSupportedException`。
- `IObservable<T>` 必须是 `System.IObservable<T>`。生成代码不引用 System.Reactive。冷流每次订阅重建请求，实现类型是 `ReactiveUI.Primitives.Advanced.FromAsyncSignal<T>`。见 [`GeneratedRequestRunner.Sending.cs`](https://github.com/reactiveui/refit/blob/92ae878250798dc103a01304704a99bb8033ed77/src/Refit/GeneratedRequestRunner.Sending.cs) 与 [`InlineRequestClassification.cs`](https://github.com/reactiveui/refit/blob/92ae878250798dc103a01304704a99bb8033ed77/src/InterfaceStubGenerator.Shared/InlineRequestClassification.cs)。
- 返回类型除 `Task` / `ValueTask` / `IObservable<T>` 外，还有 `IAsyncEnumerable<T>`、`PagedEnumerable<TPage,TItem>`、未发送的 `HttpRequestMessage`，以及编译期发现的 `IReturnTypeAdapter<TReturn,TResult>`。见 [`ReturnTypeInfo.cs`](https://github.com/reactiveui/refit/blob/92ae878250798dc103a01304704a99bb8033ed77/src/InterfaceStubGenerator.Shared/Models/ReturnTypeInfo.cs) 与 [`IReturnTypeAdapter.cs`](https://github.com/reactiveui/refit/blob/92ae878250798dc103a01304704a99bb8033ed77/src/Refit/IReturnTypeAdapter.cs)。
- 主包设置了 `IsAotCompatible`。JSON 推荐 `JsonSerializerContext`，反射 JSON 默认关闭。见 README 的 AOT 段。

`breaking-changes.md` 还记录了两处和 Observables 设计文档直接相关的行为：

- query object 按声明类型展开，不再按运行时类型。
- 展开后的键先看 `[AliasAs]`，再看内容序列化器的属性名（System.Text.Json 的 `[JsonPropertyName]`），最后才是 URL key formatter。可用 `RefitSettings.HonorContentSerializerPropertyNamesInQuery = false` 退回旧行为。

### 3.3 本仓已经接住、以及没接住的面

Refit 源码里的 attribute 包括 Observables 已有的 `Get`/`Post`/`Put`/`Delete`/`Patch`/`Head`/`Options`、`Body`、`Query`、`Header`、`HeaderCollection`、`Headers`、`Authorize`、`Property`、`AliasAs`、`Multipart`、`QueryUriFormat`、`AttachmentName`。另外还有本仓 RestAPI attribute 文件里没有的 `Url`、`Timeout`、`PathPrefix`、`QueryConverter`、`QueryName`、`FormObject`、`Encoded`、`Paged`、`PageToken`。

README 还把这些写成一等能力：JSON Lines 与 SSE 流、分页、header-aware retry、OpenTelemetry（按方法名和路由模板打 span）、测试包。它们不是 attribute 别名，而是另一层产品。

### 3.4 许可证

克隆 SHA 的 [`LICENSE`](https://github.com/reactiveui/refit/blob/92ae878250798dc103a01304704a99bb8033ed77/LICENSE) 是 MIT，版权行是 “ReactiveUI and contributors 2012 - 2026”。2026-09-25 的 [`2ecbf2a`](https://github.com/reactiveui/refit/commit/2ecbf2a5613af71d6bcc51a7440a740f814685f1) 只是把 2012 年的 MIT `COPYING` 折进 `LICENSE`，父提交的 `LICENSE` 已经是 MIT。GitHub API 报 `NOASSERTION`，是因为文件在 MIT 正文后还有 Attribution 段，不能拿 API 字段覆盖文件正文。

本仓 [`NOTICE.md`](../../NOTICE.md) 写的是 Apache-2.0 和 “Copyright (c) .NET Foundation and Contributors”，并列出 `RestMethodInfo`、`RequestBuilderImplementation`。这条 NOTICE 是 2026-06-19 的 `7ae000d` 加上的，提交说明断言当时适配的是 Apache-2.0，但没有钉 Refit 版本或 commit。本次浅克隆没有覆盖到 2026-06 的 Refit `LICENSE`，所以不能把当前 MIT 自动套到已适配代码上，也不能把 NOTICE 当成当前上游事实。

## 4. Fusillade

### 4.1 定位

官方描述：面向 `HttpClient` message handler 的 HTTP 库，做请求去重、优先级并发限制、投机后台预取，以及可选响应缓存。灵感是 Android Volley / Picasso。见 [README](https://github.com/reactiveui/Fusillade/blob/843006e90e49f2e0a6897ee838d1d6bcce9a0628/README.md) 与仓库 description。

它不生成 REST 接口，也不替换 Refit。它包在 `HttpClient` 的 handler 链上。

两个已发布包：

| 包 | 已发布 7.0.0 依赖 | 命名空间 |
|---|---|---|
| `fusillade` | [nuspec](https://api.nuget.org/v3-flatcontainer/fusillade/7.0.0/fusillade.nuspec)：`ReactiveUI.Primitives` 7.1.0、`Splat` 21.0.0、`punchclock` 7.2.0 | `Fusillade` |
| `fusillade.reactive` | [nuspec](https://api.nuget.org/v3-flatcontainer/fusillade.reactive/7.0.0/fusillade.reactive.nuspec)：`Punchclock.Reactive` 7.2.0、`ReactiveUI.Primitives` 7.1.0、`ReactiveUI.Primitives.Reactive` 7.1.0、`Splat` 21.0.0 | `Fusillade.Reactive` |

README 的 V6 破坏性变更写明：主包从 System.Reactive 栈迁到 Primitives，公共 API 仍是 handler / `HttpClient` / `Task`。R3 不是第三个 Fusillade 包；README 要求同时引用 `fusillade`、`ReactiveUI.Primitives` 和 `R3`，用 Primitives 分析器生成 `AsPrimitivesSignal` / `AsR3Observable`。这和 Observables「R3 与 Reactive 各打一个消费者包」是相反的打包策略。

默认分支 [`Directory.Packages.props`](https://github.com/reactiveui/Fusillade/blob/843006e90e49f2e0a6897ee838d1d6bcce9a0628/src/Directory.Packages.props) 已把 Primitives 钉到 8.2.0，并列出 `Akavache.SystemTextJson` 13.0.0。主项目 csproj 不引用 Akavache。Akavache 只出现在测试里，而且 `HttpSchedulerCachingTests` 有一条 `[Skip("Requires updated Akavache version to work properly")]`。当前产品缓存点是 `IRequestCache`，不是 Akavache。

### 4.2 运行时模型

[`NetCache`](https://github.com/reactiveui/Fusillade/blob/843006e90e49f2e0a6897ee838d1d6bcce9a0628/src/Fusillade/NetCache.cs) 是进程级静态入口：

- `UserInitiated`、`Background`、`Speculative`、`Offline` 四个 handler。
- 静态构造函数造一个共享的内部 `HttpClientHandler`，再包三份 `RateLimitedHttpMessageHandler`。投机预算默认 `5_242_880`（5 MB）。
- `OperationQueue` 来自 Punchclock。优先级枚举是 `Explicit = 0`、`Speculative = 10`、`Background = 20`、`UserInitiated = 100`。见 [`Priority.cs`](https://github.com/reactiveui/Fusillade/blob/843006e90e49f2e0a6897ee838d1d6bcce9a0628/src/Fusillade/Priority.cs)。
- 可用 Splat 的 `IReadonlyDependencyResolver` 替换 handler。单元测试用 `[ThreadStatic]` 覆盖。这是进程全局可变状态，不是 per-client 配置。

[`RateLimitedHttpMessageHandler`](https://github.com/reactiveui/Fusillade/blob/843006e90e49f2e0a6897ee838d1d6bcce9a0628/src/Fusillade/RateLimitedHttpMessageHandler.cs)：

- 只对 `GET` / `HEAD` / `OPTIONS` 去重。其他方法直接下发。
- 在飞请求按 `UniqueKeyForRequest` 合并。键包含 URI、方法、Accept、Accept-Encoding、Referrer、User-Agent、Authorization。然后对拼接字符串调用 `GetHashCode()`。这不是跨进程稳定哈希，也没有把碰撞当成错误。
- 字节预算耗尽后，后续投机请求直接取消。
- 若设置了 `NetCache.RequestCache`，完成时调用 `SaveAsync`。

[`IRequestCache`](https://github.com/reactiveui/Fusillade/blob/843006e90e49f2e0a6897ee838d1d6bcce9a0628/src/Fusillade/IRequestCache.cs) 的注释写明：它故意不遵守 HTTP 缓存规则。`OfflineHttpMessageHandler` 是另一条离线回放路径，构造时接收取回 body 的委托；静态 `Offline` 默认传 `null`。

所以 Fusillade 的「缓存」是应用提供的 body 存储，不是 RFC 缓存，也不是现在还绑着的 Akavache。

## 5. Primitives

### 5.1 定位

仓库创建于 2026-05-25。README 与 9.0.0 nuspec 的描述一致：面向 `IObservable<T>` 的分配敏感原语，生产依赖不含 System.Reactive 或 R3，面向 trim 与 Native AOT。见 [README](https://github.com/reactiveui/Primitives/blob/18e8a8524dfae7f1f043bdbd3d814c5e187024c0/README.md) 与 [nuspec](https://api.nuget.org/v3-flatcontainer/reactiveui.primitives/9.0.0/reactiveui.primitives.nuspec)。

已发布 `ReactiveUI.Primitives` 9.0.0 的 net8.0 依赖只有 `ReactiveUI.Disposables` 9.0.0 和 `ReactiveUI.Primitives.Core` 9.0.0。`ReactiveUI.Primitives.Reactive` 9.0.0 则依赖 Core 加上 `System.Reactive` 7.0.0。见 [该 nuspec](https://api.nuget.org/v3-flatcontainer/reactiveui.primitives.reactive/9.0.0/reactiveui.primitives.reactive.nuspec)。

这是一个包族，不是一个库。README 的包表包括 Core、Async、ObservableEvents、R3Bridge.Generator，以及 WPF / WinForms / WinUI / Uno / Blazor / Avalonia / MAUI 的 lean 与 `.Reactive` 成对包。`.Reactive` 把同一 API 编到 System.Reactive 的 `Unit` 和 `IScheduler` 上。调度抽象在 lean 包里叫 `ISequencer`，不叫 `IScheduler`。

`ReactiveUI` 26.0.1 的 net8.0 nuspec 直接依赖 `ReactiveUI.Primitives` 9.0.0、`ReactiveUI.Disposables` 9.0.0、`ReactiveUI.Primitives.Core` 9.0.0。见 [ReactiveUI nuspec](https://api.nuget.org/v3-flatcontainer/reactiveui/26.0.1/reactiveui.nuspec)。Refit 16.4.0 和 Fusillade 7 也依赖它。Primitives 的 NuGet 下载应理解为这条传递链，而不是 15 个 star 的独立采用。

版本速度：ADR-001（2026-06-28）记录的是 v5.7.0。本次已发布最新版是 9.0.0（2026-10-03）。约三个月四个 major。这仍然是不稳定信号，不是成熟替代品信号。

### 5.2 工厂方法已经补上，但名字不是 `Observable.*`

ADR-001 写「缺少 `Observable.FromAsync` / `Create` / `FromEvent`」。对 v9 源码，这句话不成立。缺的是 **同名静态类**，不是能力。

[`Signal{Factories}.cs`](https://github.com/reactiveui/Primitives/blob/18e8a8524dfae7f1f043bdbd3d814c5e187024c0/src/Primitives.Shared/Signals/Signal%7BFactories%7D.cs) 有：

- `Signal.FromAsync<T>(Func<Task<T>>)`
- `Signal.FromAsync<T>(Func<CancellationToken, Task<T>>)`，订阅结束时取消
- `Signal.FromEvent` / `Signal.FromEventPattern` 的多组重载
- `Signal.FromTask`

[`Signal{Create}.cs`](https://github.com/reactiveui/Primitives/blob/18e8a8524dfae7f1f043bdbd3d814c5e187024c0/src/Primitives.Shared/Signals/Signal%7BCreate%7D.cs) 有同步与异步 `Signal.Create`，以及 `CreateWithState`、`CreateSafe`。

README 的对照表把 `Signal.Emit` 映射到 `Observable.Return`，`Signal.None` 映射到 `Empty`，`Signal.Fail` 映射到 `Throw`。同一概念有两套名字。操作符也是扩展方法链（`Map`、`Keep`、`Throttle`），另有 Rx 别名 mixin。

README 明确警告：项目里同时有 System.Reactive 时，两边的 `Subscribe` 扩展会冲突。这和本仓 `OBS0001`（禁止同一编译同时引用 R3 与 System.Reactive）是同一类问题。Primitives 的解法是文档告诫加 `.Reactive` 孪生包；Observables 的解法是分析器直接报错。

### 5.3 事件生成器与 R3 桥

`ReactiveUI.Primitives.ObservableEvents` 是 netstandard2.0 分析器/生成器。README 写它不绑定某一种 observable 实现，按消费者已引用的提供者发射：

1. lean Primitives 的 `Signal` / `RxVoid`
2. `.Reactive` 包的 `Signal` / `System.Reactive.Unit`
3. 若没有 Primitives，则 `System.Reactive.Linq.Observable`

实例事件入口也是 `.Events()`。静态事件用 assembly attribute，生成 `RxEvents` 上的长度前缀属性名。不支持的委托报 `RXOE003`。见 README “Observable event source generation”。

R3 桥是另一个分析器包 `ReactiveUI.Primitives.R3Bridge.Generator`。README 列出 `AsPrimitivesSignal` 与 `AsR3Observable`。它是边界转换，不是把每个 IO 域生成两遍。

## 6. 三者现在是一条栈

2026-06 看，这三件事还是松散的：Refit 是 HTTP 生成器，Fusillade 是 Rx 时代的 handler，Primitives 是一个小原语库。2026-10 的已发布包已经收成一条栈：

```
ReactiveUI 26 / Refit 16 / fusillade 7
        └── ReactiveUI.Primitives
                ├── lean：无 System.Reactive、无 R3
                ├── .Reactive：System.Reactive 7
                └── R3Bridge.Generator：检测到 R3 才发射转换
```

Fusillade 仍是 handler，不进入 Refit 生成器。Refit 的 `IObservable<T>` 也不再是 System.Reactive 类型，而是 BCL 接口加 Primitives 的 `FromAsyncSignal`。谁要 System.Reactive 的操作符，得另引 `.Reactive` 包或自己转换。

## 7. 与 Observables 的对比

本仓基线：[`eng/Observables.Package.props`](../../eng/Observables.Package.props) 为 `0.4.0`；20 个消费者包；域运行时 TFM 是 `netstandard2.0;net8.0;net9.0;net10.0`（[`eng/Observables.ProjectDefaults.props`](../../eng/Observables.ProjectDefaults.props)）。R3 与 System.Reactive 版本钉在 [`Directory.Packages.props`](../../Directory.Packages.props)：R3 1.3.0、System.Reactive 6.0.1。域运行时不引用二者，见 [ADR-003](../adr/ADR-003-backend-neutral-domain-runtime.md)。

### 7.1 RestAPI 对 Refit：同源，已经不是同一代

本仓 RestAPI 是 Refit 风格的声明式 HTTP 客户端，运行时含适配代码。见 [`docs/design/restapi.md`](../design/restapi.md) 与 [`NOTICE.md`](../../NOTICE.md)。2026-06 的 Path B（PR #96）删掉了运行时反射构建器。生成器把请求建成命令式方法体，`RestService.For<T>` 只查找 `ModuleInitializer` 注册的工厂。

当前差异：

| 面 | Observables 0.4.0 | Refit 16.4.0 源码 / 已发布包 |
|---|---|---|
| 反应式返回 | R3 包发射 `R3.Observable.FromAsync`；Reactive 包发射 `SystemReactiveObservableAdapter.FromAsync` → System.Reactive。见 [`Emitter.cs`](../../Observables.RestAPI/Observables.RestAPI.SourceGenerators.Shared/Emitter.cs) 与 [`RestApiReturnTypeClassifier.cs`](../../Observables.RestAPI/Observables.RestAPI.SourceGenerators.Shared/RestApiReturnTypeClassifier.cs) | 只有 `System.IObservable<T>`，由 Primitives `FromAsyncSignal` 实现。没有 R3 包，也没有 System.Reactive 包 |
| 其他返回 | `Task` / `ValueTask` / `IApiResponse<T>` / 同步 `Return` / void。没有 `IAsyncEnumerable`、分页、`HttpRequestMessage`、返回类型适配器分支 | 上述都有。适配器由生成器按闭合类型发现 |
| query object | 设计文档写明：非集合对象整体格式化成一个值，不展开，也不对展开属性套 key formatter | V14 起按声明类型展开，并尊重 `[JsonPropertyName]` |
| 每次调用选项 | `RestApiCallScope.Push` 是本仓 0.4.0 的面，Refit 源码无同名类型 | 有 `TimeoutAttribute`、request context、header-aware retry。不是同一个抽象 |
| DI | `Observables.RestAPI.HttpClientFactory` 在 slnx 里，默认 `IsPackable=false`，不在 20 包 manifest 中 | `Refit.HttpClientFactory` 是已发布包，累计下载 1.26 亿 |
| AOT | net8+ 域运行时标了 `IsAotCompatible`。`RestApiSettings` 仍标 `RequiresUnreferencedCode` / `RequiresDynamicCode`。`FormValueMultimap`、`RestApiSettings`、`SystemTextJsonContentSerializer` 仍 `GetCustomAttribute`。`MakeGenericMethod` 只剩警告字符串，调用已不在 | 反射构建器拆出主包。推荐 source-generated JSON metadata |
| 流式 HTTP | 不做。SSE 是独立域 | REST 客户端内做 JSON 数组流、JSON Lines、SSE |
| 测试、重试、遥测 | 无对等产品包 | `Refit.Testing`、retry handler、OpenTelemetry |
| 遗留字符串 | `HttpRequestMessageOptions.InterfaceType` 仍是 `"Refit.InterfaceType"`。`JsonContentSerializer` 的 Obsolete 指向 `Refit.Newtonsoft.Json`。`AttachmentNameAttribute` 的 Obsolete 指向 `Refit.StreamPart` | 这些类型在 Refit 自己的命名空间里。Observables 包并不引用 Refit |

结论：Observables 不是 Refit 的薄封装，也还没追到 Refit 16。它保留的是 attribute 方言和一部分运行时类型；换掉的是反射构建器和反应式后端。Refit 后来自己也改成了生成优先，所以「我们比 Refit 更 AOT」不再是稳定优势。Refit 16 在查询展开、流、分页、测试和 DI 包上更完整。Observables 的不可替代点是 **一等 R3 包**、**后端中立运行时**、**`RestApiCallScope`**，以及 HTTP 只是十个域之一。

不应把 Refit 16 再合并进来。命名空间、许可证快照、反应式类型和返回类型都已经分叉。合并会把 Primitives 9 和 System.Reactive 7 拉进一个以 R3 / Rx 6 为契约的仓库，直接撞 ADR-001 和 ADR-003。

### 7.2 Fusillade：正交，而且不该吸收

本仓没有去重、优先级队列、投机字节预算或响应 body 缓存。全仓也没有 Fusillade 引用。

组合点已经存在：`RestApiSettings.HttpMessageHandlerFactory`，以及调用方自己传入的 `HttpClient`。见 [`RestService.CreateHttpClient`](../../Observables.RestAPI/Observables.RestAPI/RestService.cs)。消费者若要 Fusillade，应把 `NetCache.UserInitiated` 当作 handler 交给 `HttpClient`，而不是让 RestAPI 生成器知道优先级。

不吸收的原因：

- 它解决的是传输调度，不是接口生成。放进 RestAPI 会把 Splat、Punchclock、Primitives 变成域运行时依赖，破坏后端中立。
- 静态 `NetCache` 是进程全局可变状态。Observables 的设置是 per-client / per-call。照搬会引入跨请求串扰。
- 去重键使用 `GetHashCode()`，缓存契约明确不遵守 HTTP 缓存。这不该成为库的默认行为。
- 已发布 7.0.0 只有 406 次版本下载。没有用户信号表明该把这个模型做进 20 个包。

若将来某个应用需要「同一 GET 在飞合并」，正确边界是可选 handler 样本或文档，不是新域，也不是 RestAPI 生成器分支。

### 7.3 Primitives：工厂缺口已补，第三后端仍不成立

[ADR-001](../adr/ADR-001-primitives-backend-skip.md) 的决定是不把 Primitives 当作第三后端。它的第一条理由「没有 `FromAsync` / `Create` / `FromEvent`」对 v9 源码已经过时。其余三条仍成立，而且生态变化让第三条更强：

| ADR-001 理由 | 2026-10-24 复核 |
|---|---|
| API 不兼容 | 能力有了，形状不对。生成器要的是 `Observable.FromAsync(Func<CancellationToken, Task<T>>)` 或本仓 adapter。Primitives 是 `Signal.FromAsync`，返回 BCL `IObservable<T>`，取消语义在订阅上。Events 生成器还要 `FromEvent` 的具体重载和 `Unit`/`RxVoid` 差异。不能换成 using 别名 |
| 工程代价 | 更差了。包族含 UI 调度、Async、事件生成器、R3 桥。再加一列会把 20 包推向 30，并要第三套 `#if`。Primitives 自己用「检测符号再发射桥」而不是第三套域包 |
| 采用与稳定 | star 15。v5.7 到 v9.0 约三个月。下载来自 ReactiveUI 26 和 Refit 16 的传递依赖，不是独立 API 承诺。`.Reactive` 包依赖 System.Reactive **7**；本仓钉的是 **6.0.1**。ROADMAP R2 的 Rx 7 兼容还没做 |
| 稀释焦点 | Refit/Fusillade 改跟 Primitives，不构成 Observables 要跟的理由。R3 仍覆盖高性能冷流。再加底座会让每个域的返回类型从 2 种变成 3 种 |

另外两条 ADR 没写、但现在能从源码看出的冲突：

- Primitives README 承认与 System.Reactive 的 `Subscribe` 扩展冲突。Observables 的 Reactive 包已经把 System.Reactive 带进消费者编译。第三后端不是加法，是和新的二义性一起加法。
- `ObservableEvents` 会在「没引用 Primitives」时发射 System.Reactive，在引用 lean 包时发射 `Signal`。这和本仓「一个包一个后端、冲突即诊断」相反。Events 域若改用它，会把后端选择从包引用变成生成器探测，OBS0001 的模型就破了。

ADR 写的重新评估条件是：采用率与 API 稳定、或明确用户需求、或补齐 Rx 工厂。第三条的「能力」部分已满足，「同名工厂、可直接替换」未满足。前两条未满足。因此 SKIP 仍成立。若要让 ADR 与源码一致，应另开一次文档修订，改掉「缺少工厂」这句，并写上 v9 的 `Signal.*`、Rx 7 和 Subscribe 冲突。本次调研不改 ADR。

### 7.4 Events 对 ObservableEvents

两边都把 .NET 事件变成可订阅序列，入口都有 `.Events()`。

| | Observables.Events | Primitives.ObservableEvents |
|---|---|---|
| 产物 | 按包生成 `Observable<T>` 或 `IObservable<T>` 扩展方法 | 一个生成器，按已引用提供者选择 `Signal` 或 System.Reactive |
| UI | WPF/Avalonia 路由事件、Avalonia 附加路由事件，默认关闭 | README 展示的是 CLR 事件。UI 线程调度在另一些平台包的 sequencer 里 |
| 运行时 | 纯生成，桥接类型 `internal` | 生成器包 netstandard2.0，无固定 observable 运行时 |
| 静态事件 | 本设计文档未把 `RxEvents` 长度前缀属性作为 API | `GenerateStaticEventObservables` + 长度前缀名 |

Events 不该改接到 Primitives 生成器上。路由事件是本仓已经做完、对方 README 没有对等描述的部分。对方的提供者探测则和双包模型冲突。

## 8. 可借鉴与不应借鉴

可借鉴，而且边界清楚：

1. **钉住适配来源。** NOTICE 没有 Refit commit。当前上游是 MIT，NOTICE 写 Apache-2.0，并列出已删除的 `RestMethodInfo` / `RequestBuilderImplementation`。下一步只应是核对 2026-06 适配时的上游修订，再决定 NOTICE 保留 Apache 还是改为 MIT。不要在没钉修订之前改许可证声明。
2. **清掉指向 Refit 程序集的用户字符串。** `JsonContentSerializer` 与 `AttachmentNameAttribute` 的 Obsolete 消息把用户指到不存在于本包的类型。`Refit.InterfaceType` 这个键如果没有外部互操作测试，就是遗留兼容键，应在设计文档里写成已知遗留，而不是继续当文档事实。
3. **query object 展开是已记录缺口，不是新发现。** [`restapi.md`](../design/restapi.md) 已写 Observables 不展开 query object。Refit V14 的声明类型展开和 `[JsonPropertyName]` 键名是具体对照实现。若要补，按那个语义补，不要再引入运行时 `GetType()` 展开。
4. **AOT 注解与代码不一致。** `RestTrimAnnotations.Dynamic` 仍写 `MakeGenericMethod`，源码里已没有这个调用。反射还在 formatter / serializer。要么收紧注解，要么把 DTO 属性名在生成期烘进请求代码。后者才是 Refit 16 真正拉开的地方。
5. **返回类型适配器只在有真实包装类型需求时再做。** `IReturnTypeAdapter` 能让 `Result<T>` 这类形状不进核心生成器。本仓现在没有这个需求。不要为了对称先做。
6. **Fusillade 只作为 handler 组合示例。** 若文档要提，写「把现成 handler 交给 `HttpClient`」。不要移植 `NetCache` 静态对象，不要移植 `GetHashCode` 去重键。

不应借鉴：

- 不把 Primitives 做成 `Observables.<Feature>.Primitives`。工厂补齐没有取消包矩阵、Subscribe 冲突、Rx 7 和 major 抖动。
- 不把 R3 桥改成「检测符号再发射」。那是 Primitives 的单库策略。本仓的契约是两个显式包，冲突由 OBS0001 拒绝。
- 不把 Refit 16 的 SSE / JSON Lines 塞进 RestAPI。SSE 已是独立域。嵌进 REST 会让一个响应有两套流模型。
- 不把 retry、OpenTelemetry、分页、测试包放进当前里程碑。它们是 Refit 的产品广度，不是 RestAPI 生成器的缺口。R2 仍是 Mqtt 传输抽象，不是追 Refit。
- 不把 Fusillade 的 Splat / Punchclock / 进程静态队列引进域运行时。
- 不把 `ObservableEvents` 换成本仓 Events 生成器。会丢掉路由事件，并改变后端选择规则。

## 9. 未决

- 2026-06-19 适配 Refit 时的上游 tag / commit 没有记录。NOTICE 的 Apache-2.0 是否仍是那份代码的许可证，本次不能证实，也不能证伪。
- GitHub `pushed_at` 为 2026-10-23，三个默认分支 HEAD 都更早。未再拉取非默认分支，不知道那次 push 是 tag、文档分支还是 bot。
- `Refit.Reflection` 16.4.0 的 nuspec 本次没有单独打开。源码项目和 README 足够证明它是可选反射包，不够写它的完整依赖表。
- Primitives 3,983,331 次累计下载里，有多少来自 2026-05 仓库拆分之前的同名包，NuGet 搜索 payload 没有按时间拆开。只能确定 8.2.0 与 9.0.0 这两个 2026 版本本身就有 416,216 和 111,002 次下载，且 ReactiveUI 26.0.1、Refit 16.4.0 依赖 9.0.0。
- Fusillade 默认分支相对 7.0.0 已把 Primitives 从 7.1.0 升到 8.2.0，但还没发新 release。不要把 main 的依赖写成已发布契约。
