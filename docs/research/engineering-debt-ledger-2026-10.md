# 工程债台账（2026-10 盘点）

> 取数日期：**2026-10-24**。分支 `fix/restapi-call-scope-followups`。
> 范围：Events、RestAPI、共享层/工程治理。SignalR / Mqtt / WebSocket / Grpc / Sse / Nats / Postgres / Redis 只查共享层接缝，未逐域深挖。
> 方法：种子清单逐条代码核实（file:line）+ 三个只读子代理分域补扫。
> 背景决策：Events 与 RestAPI 保留且优先；将来接入 ReactiveUI.Primitives 第三后端（本台账不实施，只预留接缝记录）。

## 0. 状态口径

| 状态 | 含义 |
|------|------|
| 待修 | 已核实，等修复排期 |
| 待确认 | 需维护者拍板（公共契约 / ADR / 包拓扑 / 生成代码形状 / 方向选择） |
| 已修 | 本任务内已修（见 §6） |
| 出局 | 核实后不成立或不值得修 |

严重度：**P0**=真实消费者已撞到的正确性缺陷；**P1**=生成不编译代码 / 诊断失效 / 假绿通道；**P2**=潜在缺陷、验证缺口、明显文档错误；**P3**=死代码、卫生、扩展性。

---

## 1. Events 域

| # | 严重度 | 债项 | 证据 | 状态 |
|---|--------|------|------|------|
| E-1 | P0 | **CS0122：层级展开不查类型/字段对消费程序集的可见性**。`ExpandForInterfaces` 递归 `BaseType` + `Interfaces`（含跨程序集 internal 接口如 MAUI `IControlsVisualElement`）；事件本身查 `Public`，声明它的类型不查。生成代码 `global::<inaccessible>` → 消费方 CS0122。GitPulse 被迫用公开事件 adapter 绕开（GitPulse ADR-015） | `ObservableEventsGenerator.InterfacePipeline.cs:84-112`（`GetDirectBaseTypes` :184-194、`declaredEvents` :103-112）；全仓无 `IsSymbolAccessibleWithin` | 已修（§6 批次 C） |
| E-2 | P1 | **路由 `{Name}Event` 字段不查 `DeclaredAccessibility`**：private/protected/跨程序集 internal 字段直接 `ContainingType.XxxEvent` 发射 → CS0122。与 E-1 同族不同路径，建议同一修复一并过 | `ObservableEventsGenerator.RoutedDetection.cs:26-39,80-101,156-186`；发射 `ObservableEventsSyntaxFactory.cs:585-588,659-661` | 已修 |
| E-3 | P1 | **泛型接收者 `AttachedRoutedEvent*`* → CS0246**：receiver 被归一成 `OriginalDefinition`（`MyControl<T>`）但生成的扩展只声明 `<TEventArgs>`，`this MyControl<T>` 中 `T` 无绑定 | `ObservableEventsGenerator.AttachedRouted.cs:21`；`ObservableEventsSyntaxFactory.cs:329-352` | 已修 |
| E-4 | P1 | **泛型接收者路由带参重载 → CS0246**：`CreateAvaloniaRoutedExtensionMethod` / `CreateWpfRoutedExtensionMethod` 不接收 typeParameters/constraints，生成 `RoutedEvents(this MyControl<T>, …)` | `ObservableEventsGenerator.InterfaceEmission.cs:186-199`；`ObservableEventsSyntaxFactory.cs:254-313`（对照 176-184 无参重载是带参正确的） | 已修 |
| E-5 | P1 | **同一泛型约束目标混用 `.Events()`+`.EventHandlers()` → CS0101**：`ObservableEventsBootstrapExtensions_<Key>` 类未标 `partial`，两种入口各产一份同名类 | `ObservableEventsGenerator.GenericConstraints.cs:106` | 已修 |
| E-6 | P1 | **双接口同名不同签名事件仍发射**：OBS2006 告警后照常生成，`ICEvents : II1Events, II2Events` impl 无法实现 → CS0738/CS0739；现有测试只测 `new` 隐藏路径（恰好能编译） | `ObservableEventsGenerator.InterfacePipeline.cs:136-178`；`InterfaceEmission.cs:463-489` | 已修（按决策：跳过该事件） |
| E-7 | P1 | **Avalonia 非泛型 `RoutedEvent` 字段走泛型 `AddHandler<TEventArgs>` → CS1503**；且取 `EventHandler<TArgs>` 的 TArgs 不校验 `: RoutedEventArgs` 可叠 CS0311。测试 stub 全是泛型字段掩盖了此路径 | `RoutedDetection.cs:178-185,200-205`；`ObservableEventsSyntaxFactory.cs:590-603` | 已修 |
| E-8 | P1 | **泛型约束命名只取 `t.Name` 去 `I`**：`A.IFoo` 与 `B.IFoo` 各产 `IFooEvents`/`FooEventsImpl` → CS0101；arity-blind 同患 | `ObservableEventsGenerator.GenericConstraints.cs:51-59` | 已修 |
| E-9 | P2 | **测试只有手写 stub，无真实 WPF/Avalonia/MAUI 引用程序集**——E-1/E-2/E-7 正是这么漏掉的；上游 Primitives 直接加载真实程序集测试 | 测试 csproj 仅 R3/Rx/Roslyn/Verify；`GeneratorTestHarness.cs:19` 只引 `R3.Unit` 程序集 | 待修（测试基建，成本中） |
| E-10 | P2 | **静态事件半成品**：`StaticObservableEventsGenerationEnabled=false`，但静态入口一整套基础设施已在（post-init shell、语法判定、目标解析） | `ObservableEventsConstants.cs:43`；`Discovery.cs:17-33,274-301`；`ObservableEventsGenerator.cs:40,44` | 已修（删干净，§6 批次 C） |
| E-11 | P2 | **`Discovery.cs:199-268` 路由语义分支不可达**：routed/attached bootstrap stub 由 `RegisterSourceOutput` 产出（非 post-init），`GetSymbolInfo` 绑不到，`TryGetBootstrap*` 必败；实际收集全靠纯语法路径，该路径也不校验接收者可解析 | `ObservableEventsGenerator.Discovery.cs:129-141,199-268`；`ObservableEventsGenerator.cs:108-114` | 已修 |
| E-12 | P2 | **`docs/design/events.md` 多处与实现不符**：§10「跨程序集发现仅限本程序集」与 CS0122 实证矛盾；§2 类型名 `IEventsInterface_<Type>` 实为 `I{Type}Events`；§5.3 `_routes` 是 `RoutingStrategies` 非 `object?`；§5.4 参数与约束描述错；§8/:230「RoutingStrategies 以 object 传递」不实；:65/:156 空回退语义夸大；:172 方法名不存在 | `events.md:30,65,118-134,146-152,156,172,183,230,231` | 已修（全面重写） |
| E-13 | P3 | 死代码：`InterfacePipeline.cs:93-100` else-foreach 不可达；`Discovery.cs:454-458` `TypeArguments[0]` 分支；`InterfaceEmission.cs:127` 丢弃调用；`Helpers.cs:93-94,105-106` 死枚举分支 | 见左 | 待修 |
| E-14 | P3 | **`Observables.Events/targets/observables.events.props` 是源树死副本**：`_ObsIsEventsPropsProject` → `IsPackable=false`，永不进 nupkg；真正发货的是 `Package/build/Observables.Events.{R3,Reactive}.props`；README/events.md 仍指向它 | `Observables.Events.csproj:8`；`eng/Observables.ProjectDefaults.props:61,88` | 待确认（删除 or 注释） |
| E-15 | P3 | **路由 smoke 断言空转**：`routed is null` 对 `NullEvents` 结构装箱恒 false，生成全挂也能过 | `eng/nuget-smoke/Events.R3.RoutedEvents.Consumer/Program.cs:10-14` | 待修 |
| E-16 | P3 | hint/类型名标识化碰撞（`Ns.A.B` 与 `Ns.A_B` 折叠同 hint → AddSource 重名 → OBS2005）；`EmitParsedModel` 诊断 switch `_ => null` 静默吞未来 ID；`EVENTS_REACTIVE` define 无 `#if` 使用点；Reactive 测试 csproj 多引 `Microsoft.CodeAnalysis`；WpfStubs/AvaloniaStubs 三处复制 | `INamedTypeSymbolExtensions.cs:9-13`；`ObservableEventsGenerator.cs:118-129`；`Helpers.cs:72-86` | 部分已修（诊断兜底/define/测试引用；hint 碰撞与 stub 复制留） |
| E-17 | P3 | 增量管道：`Collect()+CompilationProvider` 合并 → 任何编译变更全量重解析（含全部源码渲染）；emission model 已是值相等 record 无符号捕获（✓卫生），但只有 AddSource 去重受益 | `ObservableEventsGenerator.cs:59-91`；对照 `Parse.cs`（模型全在 parse 内生成） | 待确认（重构成逐节点模型，成本大） |

## 2. RestAPI 域

| # | 严重度 | 债项 | 证据 | 状态 |
|---|--------|------|------|------|
| R-1 | P1 | ~~**值类型结果全返回形状产出不编译代码**~~：**误诊**。`SendAsync<T,TBody>` 的 `T` 无约束，`T?` 编译为 `[MaybeNull]T` 而非 `Nullable<T>`——`SendAsync<int,int>` 的 await 结果本就是 `int`，`Task<int>`/`ValueTask<int>`/sync `int`/`Observable<int>` 原样可编译（实测 `?? default` 反致 CS0019）。`RuntimeTests.INotGeneratedApi.Ping` 不生成只因缺 `[Get]`。真缺口是无测试拦截 | `RestApiBridge.cs:92`；`Emitter.cs` 三发射点；`RuntimeTests.cs` | 已修（误诊核销 + 4 形状值类型回归测试，见批次 F） |
| R-2 | P1 | **`ref`/`out`/`in` 参数被丢弃**：`{param.Type}{?} @{name}` 不发射 RefKind/params/默认值 → 生成类不实现接口（CS0535/CS0738）；`FormatSendArgs` 还会把 `out` 参数按名传入 | `Emitter.cs:493-496,417-430`；Parser 无 RefKind/IsParams 校验 | 待确认（先加诊断拒绝 or 支持？） |
| R-3 | P2 | **enum query/form 格式化对 `[Flags]` 组合与未定义值崩溃**：`GetMember(val).First()` 空数组 → `InvalidOperationException`（且在 `ConcurrentDictionary.GetOrAdd` 工厂内，每调必崩） | `RestApiSettings.cs:336-339,401-404` | 已修（`FirstOrDefault` + 5 回归测试） |
| R-4 | P2 | **自定义 `HttpMethodAttribute` 子类的 `Method` 重写被忽略**：沿基类链匹配 7 个内置动词名，失败退化类名去 `Attribute` 后缀 → `SEARCH` 变 `SEARCHATTRIBUTE`；`Method` 属性值从不读。特性未 sealed，子类化是允许路径 | `Parser.Http.cs:269-297`；`restapi.md:54-58` 声明可子类化；`Attributes.cs` 动词特性非 sealed | 待确认（支持 `Method` 求值 or 密封 or 文档声明限制） |
| R-5 | P2 | **`RestTrimAnnotations.Dynamic` 字符串提 `MakeGenericMethod`，运行时已无此调用**；同串由 Emitter 发进每个生成代理 | `RestTrimAnnotations.cs:9-10`；`Emitter.cs:81-82`；反射实际剩 formatter/serializer `GetCustomAttribute` | 待修（文案；生成代码形状变 → Verify 快照） |
| R-6 | P2 | **`RestService.For` 十个重载挂 `PublicMethods|PublicProperties` DAM，比 Path B 字典查找实际需要宽**（`TryGetGeneratedFactory` 只做查表 + `GetGenericTypeDefinition`，零成员反射）；`restapi.md` §11 说「仍用 `All`」是另一种过时 | `RestService.cs:32-35,59-62,105-108,122-124,139-141,168-170,185-187,238-240,258-260,290-292`；`restapi.md:354` | 待确认（收窄/删除注解 = 公共契约 + PublicAPI 基线） |
| R-7 | P2 | **NOTICE.md 许可证错误 + 类型清单过时**：写 Apache-2.0 / ".NET Foundation and Contributors"；2026-06-01 适配 commit `3929a95` 时 Refit 最新 release 为 v10.1.6（2026-03-21，`LICENSE`=MIT "Copyright (c) ReactiveUI 2012 - 2025"），git 历史全段 MIT。清单还列着 Path B 已删的 `RestMethodInfo`/`RequestBuilderImplementation` | `NOTICE.md:9-17`；refit `v10.1.6:LICENSE`；`restapi.md` §2.1 删除表 | 已修（NOTICE→MIT + v10.1.6 基线） |
| R-8 | P2 | **`docs/design/restapi.md` 实质错误 ≥5 处**：:17 同 Apache-2.0 错；§6/:280 OBS3005 描述反（实际 R3 侧 `IObservable<T>`→OBS3003，OBS3005 在 Reactive 侧缺 adapter 时发）；§2.2/:39「无 Activator.CreateInstance 回退」不实（Emitter.cs:154 泛型接口用 `Activator.CreateInstance`+`MakeGenericType`）；§3.2/:73 `[Authorize]` 语义错（实际用参数值作 token，非 `AuthorizationHeaderValueGetter`）；§4-5 生成样例整体过期（缺 `______specN`/`RestApiCallScope`/`in MethodSpec` 重载）；§4.3 仍写死模型 `PathFragmentModel` | `restapi.md:17,39,73,153-200,231-237,259-271,280,295,354`；`RestApiReturnTypeClassifier.cs:74-92`；`Emitter.cs:154` | 已修（文档） |
| R-9 | P2 | **query object 不展开**：非集合对象整体 `FormatQueryValue` 成单值；Refit V14 起按声明类型展开且尊重 `[JsonPropertyName]`。文档已承认为缺口（§3.4:122） | `RestApiProtocol.cs:120`；`RestApiBridge.cs:46-52` | 待确认（功能缺口，排期待定） |
| R-10 | P2 | **无 `IAsyncEnumerable<T>` 流式返回**：ReturnTypeInfo 无 streaming kind → OBS3003 | `RestApiReturnTypeClassifier.cs`；`Models/MethodModel.cs:27-29` | 待确认（功能缺口，ROADMAP 未排） |
| R-11 | P2 | **JSON source-gen 未完成**（ROADMAP R2-B1）：运行时已有 `TypeInfoResolver` 钩子 + 反射 fallback suppression，缺生成侧 context 支持 | `SystemTextJsonContentSerializer.cs:163-179`；`restapi.md:353` | 待确认（排期项） |
| R-12 | P3 | **死代码/死模型**：`PathFragmentModel`（整文件，restapi.md §4.3 还当现行机制）；`PushStreamContent`/`NameValueCollection`/`AnonymousDisposable`/`EnumerableExtensions.TryGetSingle`/`EmptyDictionary`/`ValueStringBuilder` 零调用；`HttpContentExtensions` CT 重载 polyfill 无调用方；`MethodModel.IsApiResponse` 与 `ParameterModel` 十余字段写而不读；`SendAsync` 6 参重载 `isApiResponse` 被忽略（`RestApiBridge.cs:63,76`） | `PathFragmentModel.cs`；`RestApiBridge.cs:63,76,92`；`MethodModel.cs:16`；`ParameterModel.cs:7-18` | 部分已修（死文件+内部模型字段删；`SendAsync` 6 参 shim 属 public API 留确认） |
| R-13 | P3 | **`"Refit.InterfaceType"` 死 key**：`HttpRequestMessageProperties.cs` 固定字符串，无任何读写方；调研稿 §8 建议记为已知遗留 | `HttpRequestMessageProperties.cs:6-11` | 待确认（删 or 文档标注遗留） |
| R-14 | P3 | **`Observables.RestAPI/targets/` 死构建文件**：`observables.restapi.props` 为空 `<Project/>`，`.targets` 是 Refit 时代 PackagesConfig 检查；无引用、不打包 | `Observables.RestAPI/targets/` | 已修（删除） |
| R-15 | P3 | 杂项：`[HeaderCollection]` 无类型校验（非 IEnumerable 静默丢 header，`RestApiProtocol.cs:291-292`）；`[Body]`+`[Multipart]` 组合无校验（Body dispose multipart content 后仍写入，`RestApiProtocol.cs:136-152`）；接口级 `[Headers]` 不随继承（`Parser.Http.cs:86-96`）；`[AliasAs]` 不映射 Multipart/Body/HeaderCollection 名（`Parser.Http.cs:217-224`）；返回类型 `Task` 只按名匹配不查命名空间（`RestApiReturnTypeClassifier.cs:40`）；`IsUnitType` 名+命名空间字符串匹配可误判（`RestApiBridge.cs:644-646`）；`[Authorize]` null 值产 `"Bearer "` 尾空格头（`RestApiProtocol.cs:130`）；`Parser.cs:27` 用 `SyntaxTrees[0]` 的 ParseOptions 判 LanguageVersion；`UriFormat.SafeUnescaped` 按转义处理（`RestApiBridge.cs:570`） | 见左 | 部分已修（Authorize null/IsUnitType/Task 命名空间/Headers 继承 4 项；诊断类留排期） |
| R-16 | P3 | `AddRestApiClient` `settingsAction` 重载把工厂包成 singleton —— 每 T 仅首解析调用一次，用户工厂内解析 scoped 服务会被钉住 | `HttpClientFactoryCore.cs:17` | 待确认（设计取舍） |
| R-17 | P3 | 测试缺口：`Task<int>`/ref/out、自定义 `HttpMethodAttribute` 子类、`[Flags]`/未定义 enum、`[Body]`+`[Multipart]`、派生 `[Headers]` 均无例；GeneratorTests 快照仅 3 个 | `RuntimeTests.cs`；`GeneratorTests/` | 待修（随缺陷修复补例） |

## 3. 共享层 / 工程治理

| # | 严重度 | 债项 | 证据 | 状态 |
|---|--------|------|------|------|
| S-1 | P1 | **OBS0001 对 RestAPI 与 Events 是死规则**：`HasObservablesR3Backend` 只认程序集 `Observables.<D>.R3`/`*.R3.SourceGenerators` 或命名空间 `Observables.<D>.Generated`——RestAPI 包只 ship `Observables.RestAPI.dll` 且生成命名空间是 `Observables.RestAPI.Implementation`；Events 双包零 lib。两域同装 R3+Reactive 时用户只见裸 CS0101/CS0121 无 OBS0001 | `ReactivePackageConflictDetection.cs`；`Observables.RestAPI.R3.csproj`（仅一个 lib ref）；`Emitter.cs` 输出 `Observables.RestAPI.Implementation` | 已修（批次 F：声明式后端键 + 符号兜底 + 三后端模型） |
| S-2 | P1 | **`ci.yml` `shared` paths-filter 漏 `Observables.SourceGenerators.SharedSource.props`**：改这份 props（加/删共享源）→ `shared=false` → 全域测试/pack 跳过 = 假绿；`PackPublishGateTests.cs:25-32` 把不完整清单固化成断言；slnx `/Solution Items/` 也未列 | `.github/workflows/ci.yml:190-205`；`eng/Observables.Build.Tests/PackPublishGateTests.cs:25-32`；`Observables.slnx:15-17` | 已修 |
| S-3 | P2 | **Published 模式 smoke 可被本地陈旧 nupkg 遮蔽**：`nuget.config` 把 `local`（`../../artifacts/package`）列在 nuget.org 前且无 packageSourceMapping；README:11 自称只用 nuget.org | `eng/nuget-smoke/nuget.config`；`eng/nuget-smoke/README.md:11` | 待确认（需实际跑验证概率） |
| S-4 | P2 | **`ForbiddenBackendAssemblies` 对未知包后缀返回空集**：R2-A3 的 `Observables.Mqtt.MQTTnet4/5` 中立适配包、将来 `.Primitives` 都需要新臂；当前零校验静默放过 | `build/PackCsprojReader.cs:100-114`；`BackendPackageIds:14-18` | 待确认（随三后端/Mqtt 适配包一起做，先设计记录） |
| S-5 | P2 | **`ObservableReturnTypeParser` 桥接检查不对称**：只在 `IObservable` 分支校验 `reactiveAdapterMetadataName`，R3 分支不校验 → 桥缺失时裸 CS 错误而非诊断；外部后端拒绝硬编码两类型 | `ObservableReturnTypeParser.cs:73-102` | 待确认（三后端重构点） |
| S-6 | P2 | **`ROSLYN_4`/`DOMAIN_<BACKEND>` 常量散落各生成器 csproj**：新项目忘定义 `ROSLYN_4` → 共享管线整体 `#if` 成空壳、零报错；`ROSLYN_4` 名字本身失真（仓内只有 Roslyn 4.12） | 各 `*.SourceGenerators.csproj`；`GeneratorFailSafe.cs` 等 `#if ROSLYN_4` 包裹 | 已修 |
| S-7 | P2 | **TrimTests 只覆盖 R3 后端**且注释自称 "all 9 IO domain runtimes"（实为 8 域+RestAPI、仅 R3 路）；Reactive 路 trim 正确性零覆盖 | `Observables.TrimTests.csproj`；`Program.cs:4-6` | 待修（补 Reactive 引用块或改注释；三后端前必修） |
| S-8 | P2 | **`_ObsIsDomainFolder` 硬编码 9 个域文件夹**：新域文件夹静默丢多 TFM/IsPackable/PublicApi 默认集 | `eng/Observables.ProjectDefaults.props:29-37` | 已修 |
| S-9 | P3 | 双份硬编码清单：`DefaultLibTfms` vs `ObservablesRuntimeTargetFrameworks`；`TestProjectTfms` 单 TFM 列表 vs `_ObsIsSingleTfmTestProject`；`AnalyzerFolder` vs `ObservablesAnalyzerRoslynFolder`；`ObservablesPackGeneratorOutputDir` 硬编码 ns2.0 ×20；smoke 消费者各钉 R3/Rx 版本 ×23 无同步测试；`sync-doc-loc.ps1` 硬编码 20 项目；`CodeFixSyntaxHelper.cs:83-86` 硬编码 5 动词 | `build/PackCsprojReader.cs:6-12`；`eng/Observables.ProjectDefaults.props`；`eng/scripts/sync-doc-loc.ps1:23-44` | 待修（逐步收敛单一真相源） |
| S-10 | P3 | `EmptyProxyInterfaceAnalyzer.cs:29-32` 不排除嵌套开放泛型接口 → `Outer<T>.IInner` 双诊断；`ProxyDomainCatalog.cs:99-115` 事件不计成员 → 仅事件接口双诊断（空接口+不支持成员） | 见左 | 待修 |
| S-11 | P3 | `CsprojPackageReferenceEditor` 不处理 XML 命名空间/`Update`/`VersionOverride`/条件引用；`ProxyDomainTable.FormatAttribute` 死分支（`[`+InsertText 无闭合）；`ReactivePackageConflictDetection.cs:33` analyzer 程序集检查近死 + `HasNamespace` 假阳性面；`IoProxyModelAssembly.cs:90-103` null handler 静默丢成员；`IoProxyInterfaceWalk` 签名忽略 arity；`IoProxyGeneratorPipeline.cs:38` 无意义重包；`IoProxyPipelineMeasurement` 可变静态留在生产路径；`GeneratedSourceHeader.LeadingTrivia` 固定 `#nullable enable` 与 `WritePrefix` 不对称；`ObservableReturnTypeParser.cs:6` 全文件唯一 `public`；`IdentifierHelper.GetSafeHintName` 残留 `,`/空格/`?`；`GeneratedProxyFactoryRegistry.cs:25` 重复注册静默覆盖；`IsE2ETestProject` 把纯单测项目归串行组；`.g.cs` 结尾用户文件被豁免 CS 错误 | 见左 | 待修 |
| S-12 | P3 | docs/design/contributor.md:118 死链 `docs/plans/DocumentationDrivenDevelopmentBootstrap.md`；`Observables.Roslyn.Shared`/`Observables.R3.Runtime` 两目录在 AGENTS 表与 slnx 中缺席；pack 项目命名两种风格（`.R3.csproj` vs `.R3.Pack.csproj`）；benchmarks 只测 R3 生成器 | `contributor.md:118`；`Observables.slnx` | 待修 |
| S-13 | 待确认 | **ADR-001 Supersede**：理由 1「Primitives 缺 FromAsync/Create/FromEvent」对 v9 不成立（`Signal.FromAsync`/`Create`/`FromEvent` 已补）。需新 ADR 换真实理由重写决策。**注意编号冲突**：README 下一可用号 ADR-004，但 ROADMAP R2-A1 已把 ADR-004 预留给 Mqtt 传输抽象——需协调谁先用 | `docs/adr/ADR-001`；`docs/adr/README.md:19`；`docs/ROADMAP.md` R2-A1 | 已修（ADR-005 Supersede；ADR-004 仍留 Mqtt） |
| S-14 | 待确认 | **OBS0001 三后端语义**：两两互斥（3 对冲突）还是三选一（同域至多 1 个后端包）？与 S-1 修复联动，先出设计记录再动代码 | `ReactivePackageConflictAnalyzer.cs`；`ProxyDomainTable.cs` 两包模型 | 已修（obs0001 设计 + 批次 F 实施：`ProxyDomainTable` 三槽 `BackendKind`，Primitives 槽预留） |
| S-15 | 待确认 | **System.Reactive 6.0.1 钉版 vs ReactiveUI.Reactive 依赖 Rx 7.0.0**（ROADMAP R2-B2）：Primitives 后端落地前须先解 Rx 7 兼容 | `Directory.Packages.props:11` | 待确认（排期项） |
| S-16 | 待确认 | **AGENTS.md 命名表加第三行** `Observables.<Feature>.Primitives`（与 .R3/.Reactive 同级）：用户已批方向，文档先行 | `AGENTS.md:24-27,56-59` | 已修 |
| S-17 | P3 | 工作树卫生（只报告）：`.hermes/plans/`（规划工具残留，untracked）；`C:CodeSkymlyObservablespackverify-0.3.1.log`（566 行 Nuke 日志，重定向无引号产生的畸形文件名）；`.nuke/build.schema.json` EOL-only 改动；两份未跟踪调研稿（即本任务依据的两份）。均不清理 | `git status` | 已记录 |

## 4. Primitives 三后端接缝清单（预留，不实施）

### 4.1 已知前置障碍（台账登记）

| # | 障碍 | 证据/出处 |
|---|------|-----------|
| P-1 | `ReactiveUI.Primitives` 传递 `R3Bridge.Generator` analyzer，其公开 marker attribute 曾致 Refit 跨项目 CS0436（refit#2176/#2194）；Refit 用 `RemoveReactiveUIPrimitivesR3BridgeAnalyzers` target 剔除——`.Primitives` 打包时须等价处理 | refit `src/Directory.Build.targets:23-31`；调研稿 §1.3 |
| P-2 | Primitives `Subscribe` 扩展与 System.Reactive 同名 → 同作用域 CS0121；决定 `.Primitives` 与 `.Reactive` 是否/如何共存（喂给 S-14） | Primitives README；调研稿 §5.2 |
| P-3 | `Signal<T>` 取消语义基于 subscription，与 R3 `Observable<T>`/`IObservable<T>` 不等价——新 ADR 论证材料 | 调研稿 §7.3 |

### 4.2 代码接缝（将来 `.Primitives` 必须触碰）

1. **生成器编译开关**：`Observables.SourceGenerators.props:6`（默认 backend）+ 新增 `Observables.SourceGenerators.Primitives.props`（镜像 `.R3.props`，注入 `OBSERVABLES_PRIMITIVES`）；`BackendTokens.cs:10-29` `IsR3` bool → 三态 + `QualifyGeneratedNamespace` 第三形态；各域 `*.SourceGenerators.csproj` DefineConstants 加 `<DOMAIN>_PRIMITIVES`（顺手收 `ROSLYN_4` 进 props，治 S-6）；各域 shproj `#if <D>_R3` 加 `#elif <D>_PRIMITIVES`。
2. **Analyzer/CodeFix**：`ReactivePackageConflictAnalyzer`+`ReactivePackageConflictDetection`+`OBS0001` 文案（冲突矩阵 1→3 对，并修 S-1 死规则）；`ProxyDomainTable` 的 `ReactiveAssemblyName`/`R3PackageName`/`R3PackageByReactivePackageId`/缺包诊断语义；`AddRuntimePackageReferenceCodeFixProvider.cs:27-28,65-87`（两包字典）；`SwitchToReactivePackageCodeFixProvider.cs:37-52`（单向→N×N）；`ObservableReturnTypeParser.cs:73-102`（按后端拒绝其余两种）。
3. **打包/验证**：`PackCsprojReader.cs:14-18`（`BackendPackageIds` 加 `ReactiveUI.Primitives`）+ `:100-114`（`ForbiddenBackendAssemblies` 加 Primitives 臂 + 中立适配包规则）；`eng/Observables.BuildManifest.json`（+10 包条目/test/smoke）；每域 `*.Package/` 新增 `Observables.<Feature>.Primitives.csproj`；`eng/PackRoslynAnalyzer.targets` 已通用零改动。
4. **CI/smoke/trim**：`ci.yml:190-205` shared filter 加 Primitives.props（并顺手补 SharedSource.props 治 S-2）；`eng/nuget-smoke/` 每域 `*.Primitives.Consumer` + `FalseGreenCiGateTests` InlineData；`Observables.TrimTests` 加引用块（先治 S-7）；`.github/ISSUE_TEMPLATE/generator_change.yml:31-40` backend 下拉。
5. **文档**：`AGENTS.md`（命名表、检查清单、包数）；`CONTEXT.md:3,7-9`；`docs/design/architecture.md` §3 双后端整节；`contributor.md` 检查清单；`README.md` 多处两包表述；`ROADMAP.md` 20→30 包；`docs/DOCUMENTATION.md`；`eng/nuget-smoke/README.md`。

## 5. 建议修复顺序（影响 × 成本）

| 序 | 批次 | 内容 | 理由 |
|----|------|------|------|
| 1 | 文档错直修（低成本先行） | R-7 NOTICE 改 MIT+钉适配基线+更新类型清单；R-8 中 NOTICE 许可证一句；E-12 events.md 全面修正；R-8 restapi.md ≥5 处实质错误 | 法律/事实正确性，零风险 |
| 2 | 明确 bug 直修 | S-2 ci.yml shared filter + PackPublishGateTests；E-15 路由 smoke 断言；S-8 `_ObsIsDomainFolder` 模式化；S-6 `ROSLYN_4` 收进 props | 假绿/静默失败通道，小修 |
| 3 | 先汇报后动 | E-1+E-2 可见性过滤（CS0122 根治，生成器形状变）；E-3~E-8 生成不编译缺陷逐个修+回归测试；R-1 值类型返回（null 语义决策）；R-2 ref/out（诊断 or 支持） | 生成代码形状/诊断/语义决策 |
| 4 | 先汇报后动 | S-1 OBS0001 死规则修复 + S-14 三后端模型设计记录（合并做）；S-13 ADR-001 Supersede（编号归属先定）；S-16 AGENTS 第三行随 ADR | 公共契约/ADR |
| 5 | 运行时小修 | R-3 enum 格式化崩溃；R-15 各杂项按需；E-11/E-13 死代码删除；R-12 死代码（PublicAPI 联动处先报） | 独立小修 |
| 6 | 结构性 | E-9 真实程序集测试基建；E-10 静态事件去留；R-9/R-10/R-11 功能缺口排期；E-17 增量重构；S-3 smoke config 验证修复；S-4 Forbidden 规则设计；S-7 TrimTests；S-9 单一真相源 | 中-大成本，多数要排期 |

## 6. 已修项

> 本轮执行批次 A+B+E 全量、C（Events E-1/E-2/E-3~E-8）、D（设计记录+ADR 草稿）。测试基线：Events 双路生成器测试 47/47（新增 13 条回归）、RestAPI 生成器测试 49/49、RestAPI 运行时 79×3TFM + 11×3TFM、eng 构建测试 73/73。

### 批次 A — 文档直修

| # | 修复 |
|---|------|
| R-7 | `NOTICE.md` 改为 MIT + 钉适配基线 Refit **v10.1.6**（`3929a95` 时点最新 release）+ 删除 Path B 已删类型清单；`PushStreamContent` 删除后仅剩 MIT 一个归属 |
| R-8 | `docs/design/restapi.md`：许可证表述、OBS3005 归属侧、Activator 回退、`[Authorize]` 语义、生成样例（`______specN`/`RestApiCallScope`）、`PathFragmentModel` 死模型、§11 DAM 描述 |
| E-12 | `docs/design/events.md` 全面重写：§10 跨程序集发现改为「可发现但按可访问性过滤」、真实生成形状（`I{Type}Events`/`RoutedEvents` 重载签名/约束/路由检测条件/静态事件已删除） |
| R-14 | `Observables.RestAPI/targets/` 两个 Refit 时代死文件删除 |

### 批次 B — 明确 bug

| # | 修复 |
|---|------|
| S-2 | `ci.yml` shared filter 补 `Observables.SourceGenerators.SharedSource.props`；`PackPublishGateTests` 断言同步 |
| E-15 | `eng/nuget-smoke/Events.R3.RoutedEvents.Consumer/Program.cs` 改为断言 `.Click` 成员访问（`NullEvents` 装箱恒 false 空转消除） |
| S-8 | `eng/Observables.ProjectDefaults.props` `_ObsIsDomainFolder` 改 `/Observables.` 前缀模式匹配 |
| S-6 | `ROSLYN_4` 集中进 `Observables.SourceGenerators.props`；19 个生成器 csproj 去重（`EVENTS_R3`/`RESTAPI_REACTIVE`/`MQTT_R3` 等域常量保留） |

### 批次 C — Events 生成器（根治生成不编译代码一族）

| # | 修复 |
|---|------|
| E-1 | `IsSymbolAccessibleWithin` 过滤声明事件（含跨程序集 internal 接口）；新增 `IsTypeNameable`/`IsEventSignatureNameable` 递归签名可命名闸门（泛型实参/数组/tuple/嵌套）；种子类型不可访问则不生成；`BuildWillEmitInterfaceSet` 防子接口引用未发射父接口；访问器三级决策：`_sender.E` 直连（runtime 成员类型兼容且经 receiver 可达）→ `((DeclType)_sender).E` cast → 跳过。`runtimeEvents` 改收全部实例事件以正确处理 protected/private 遮蔽 |
| E-2 | 三处路由 `{Name}Event` 字段检测加 `IsSymbolAccessibleWithin`（WPF/Avalonia/CLR-fallback 路径）；不可达字段不收录为该事件的路由路径 |
| E-3 | `AttachedRoutedEvent*`：接收者 `OriginalDefinition` 的类型参数先于 `TEventArgs` 声明于生成方法，约束复用 `CreateSourceTypeConstraintClauses`；接收者 `TEventArgs` 撞名时改 `TArgs`；接收者不可访问则不生成 |
| E-4 | `RoutedEvents`/`RoutedEventHandlers` 泛型接收者：三条扩展工厂（Avalonia/WPF/FromSender）均接收 `typeParameters`+`constraintClauses` |
| E-5 | 泛型约束 bootstrap 类 `ObservableEventsBootstrapExtensions_<Key>` 加 `partial`，`.Events()`+`.EventHandlers()` 两文件同名类合并 |
| E-6 | 按决策：**跳过冲突事件**。多父接口同名不兼容事件→冲突父退出基类列表、非冲突事件扁平化进本子接口、冲突事件记 `SkippedConflictingEvents` 报 OBS2006 |
| E-7 | Avalonia 非泛型 `RoutedEvent` 字段走 `(RoutedEvent, Delegate,…)` 重载；泛型字段 `TArgs` 与 handler `TArgs` 均校验 `: RoutedEventArgs`，不满足回退 CLR 订阅 |
| E-8 | 约束组合命名：`A.IFoo`/`B.IFoo` 冲突时才用命名空间前缀（`IA_FooEvents`），无冲突保持短名（与 `ResolveInterfaceNameCollisions` 惰性消歧一致）；同时防遮蔽层级接口名 |
| E-10 | 静态事件路径整体删除：`StaticObservableEventsGenerationEnabled`、post-init `ObservableEventsStatics` shell、`IsStaticEventsEntryMemberAccess`、`TryGetStaticObservableEventsTargetFromMemberAccess`、`TryGetTypeFromObservableEventsStaticsNested`、stable-hint 恢复链（`TryResolveNamedTypeByStableHint`/`Enumerate*`） |
| E-11 | `Discovery.cs` 路由/attached 语义绑定死分支删除（语法路径 `TryCollectRoutedTargetsFromInvocationSyntax` 已是唯一收集者且在其之前） |
| E-13 | `InterfacePipeline` else-foreach 死分支、`Discovery` `TypeArguments[0]` 错绑分支、`Helpers` AttachedRouted 死枚举分支、`InterfaceEmission:127` 丢弃调用 |

### 批次 E — 运行时小修 + 死代码

| # | 修复 |
|---|------|
| R-3 | `RestApiSettings` 两处 `GetMember(val).First()` → `FirstOrDefault()?`；新增 `FormatterTests` 5 例（`[Flags]` 组合/未定义值/EnumMember 值，URL+Form formatter） |
| R-12 | 删除：`PathFragmentModel`（+`.projitems` 同步）、`PushStreamContent`、`AnonymousDisposable`、`EnumerableExtensions`、`NameValueCollection`、`ValueStringBuilder`；`MethodModel.IsApiResponse` 与 `ParameterModel` 11 个写而不读字段移除；`loc/zh-Hans` 同步清理（含已死的 `DelegatingStream` 残留）。**更正**：台账原列 `HttpContentExtensions` 「无调用方」**误判**——它是 netstandard2.0 `ReadAs*Async(ct)` polyfill，已恢复 |
| R-15 | 已修：`[Authorize]` null 不再产 `"Bearer "` 头；`IsUnitType` 加 `IsValueType` 校验；`Task`/`Task<T>`/`ValueTask<T>` 分类加 `System.Threading.Tasks` 命名空间校验；接口级 `[Headers]` 改走 `ContainingType`+`AllInterfaces`。**未修留排期**：`[HeaderCollection]`/`[Body]`+`[Multipart]`（需诊断设计）、`[AliasAs]` multipart 映射（Refit 对齐问题）、`SafeUnescaped`（语义待定）、`SyntaxTrees[0]`（复核为惯例可接受） |
| E-16 | `EmitParsedModel` `_ => null` 改为未映射 ID 报 OBS2005（`unmapped diagnostic id`）不再静默吞；`EVENTS_REACTIVE` define 删除；Reactive 测试 csproj 多余 `Microsoft.CodeAnalysis` 引用移除。**留**：hint 标识化碰撞（E-16 主干，需 GetSafeHintName 重设计）、WpfStubs 复制（纯卫生） |

### 批次 D — 设计记录与 ADR

| # | 产出 |
|---|------|
| S-13 | 新建 `docs/adr/ADR-005-primitives-backend-adopt.md`（Accepted，Supersedes ADR-001）；ADR-001 标 `Superseded by ADR-005`；README 索引同步；ADR-004 保持留给 Mqtt 传输（ROADMAP 预留） |
| S-14 | 新建 `docs/design/obs0001-backend-conflict.md`：**两两互斥**模型（同域 ≥2 后端 = OBS0001，含 `.Primitives`×`.Reactive` CS0121 理由）；方案改为 MSBuild 声明式后端键（`buildTransitive` props 写 `Observables<Feature>Backend_<Backend>`），符号启发式仅作 ProjectReference 兜底 |
| S-1 | 死规则根因已入设计记录 §1（三检测腿全灭的证据链）；修复随 OBS0001 重设计排期 |
| S-16 | `AGENTS.md` 命名表/项目组成/后端规则三处加 `.Primitives` 行（标「未发布/未实现」）；互斥规则改为「三个后端包两两互斥」 |

### 批次 F — OBS0001 三后端实施 + R-1 核销

| # | 修复 |
|---|------|
| R-1 | **误诊核销**：实测证明无约束 `T?` 不产生 `Nullable<T>`；未改 `Emitter` 发射形状。回归补进 `RuntimeTests`（`IValueResultApi`：`Task<int>`/`ValueTask<int>`/sync `int`/`Observable<int>` 生成+运行）与 `CoreGeneratorTests`（断言生成代码不含 `??`）。RestAPI.Tests 84×3TFM、Reactive.Tests 11×3TFM、GeneratorTests 54 全绿 |
| S-1 + S-14 | 按 obs0001 设计记录落地：① 10 域 × 2 包 `build/*.props` 写入 `Observables<Feature>Backend_<R3/Reactive>=true` + `CompilerVisibleProperty`（`eng` 新增 `BackendKeyPropsTests` 20 断言）；② `ProxyDomainTable` 扩 `BackendKind` 三槽（R3/Reactive/Primitives）+ `BackendKeyName`/`BackendPackageName`/`BackendMarkerMetadataName`，`R3PackageByReactivePackageId` → `DomainByBackendPackageId`+`AllBackendPackageIds`；③ `ReactivePackageConflictDetection` 重写为「`build_property.*` 声明键 OR 符号兜底（桥接程序集名/生成 marker 元名）」逐后端探测，同域 ≥2 → OBS0001 参数化列出具体包名；④ RestAPI 双路生成器常发 `R3BackendMarker`/`ReactiveBackendMarker`（Events 复用既有 bootstrap 类型作 marker）；⑤ CodeFix：`AddRuntimePackageReference` 注册三后端包动作、`SwitchToReactive` 改「保留 Reactive 移除其余兄弟包」；⑥ `AnalyzerReleases.Unshipped.md` 登记行为变化。Analyzers.Tests 79、CodeFixes.Tests 48、eng Build.Tests 93 全绿 |

### 决策登记（本轮拍板）

- ~~**R-1 null 语义**~~：**作废——R-1 系误诊**。无约束泛型 `T?` 是 `[MaybeNull]T` 而非 `Nullable<T>`，`SendAsync<int,int>` await 结果就是 `int`，原生成代码本可编译；`?? default` 实测致 CS0019。最终处理：不改发射，补 `Task<int>`/`ValueTask<int>`/sync `int`/`Observable<int>` 生成+编译+运行回归。
- **R-12 public 面**：`SendAsync` 6 参 `isApiResponse` shim 为 public API，按约定未删，列入待确认清单（下方 §7 补）。

### 新增回归测试（Events，双路各 13 条）

跨程序集 internal 接口（`ExtraReferences` 编译内嵌程序集）、显式接口事件 cast 访问器、`new` 遮蔽基类 cast、同程序集 internal 事件、泛型接收者 WPF/Avalonia 路由、非泛型 `RoutedEvent` 字段、`TArgs` 非 `RoutedEventArgs` 回退、约束双入口 partial 合并、跨命名空间同名约束限定名、双父同名冲突 OBS2006、protected 路由字段排除、静态入口零发射。

## 7. 局限

- Events 扫描代理对「语义分支不可达」（E-11）的结论基于 stub 发射路径推断；R-1/R-2「不编译」由发射文本×签名推演——修复前建议各写一条最小复现（`Task<int>` 接口、含 `ref` 参数的接口）进 GeneratorTests 验证。
- S-3 Published smoke 遮蔽为配置面推断，未实际跑 Nuke 验证。
- 8 个 IO 域未逐域扫；其 `.R3` 桥运行时、E2E 与域内 `#if` 不在本轮范围。
