# RestAPI 域 — 开发设计文档

> 状态：**已实现**（Path B 编译期代码生成）。NuGet 包 `Observables.RestAPI.R3` 与 `Observables.RestAPI.Reactive` 的发布记录见 [CONTRIBUTING.md](../../CONTRIBUTING.md)。
> 命名、打包、诊断分段等约定以仓库根 [`AGENTS.md`](../../AGENTS.md) 为权威，本文在其框架内细化 RestAPI 域。

## 1. 目标与定位

将 **HTTP REST 边界**通过 Roslyn 源生成器桥接为反应式流：声明式接口（Refit 风格特性）→ 编译期生成 `HttpClient` 代理实现 → 运行时直接调用，无反射。

| 返回类型 | 后端 | 语义 |
|----------|------|------|
| `Task<T>` / `ValueTask<T>` | 通用 | 一次性请求/响应 |
| `Task` / `ValueTask` | 通用 | 发送请求并检查状态，跳过响应体反序列化 |
| `Observable<T>` | R3 包 | 单次响应冷流（`Observable.FromAsync`） |
| `IObservable<T>` | Reactive 包 | 单次响应冷流（`SystemReactiveObservableAdapter.FromAsync`） |
| `IApiResponse<T>` / `IApiResponse` | 通用 | 包装响应头、状态码和错误。泛型包装还提供反序列化后的内容 |

RestAPI 域的运行时部分包含由 [Refit](https://github.com/reactiveui/refit)（MIT，适配基线 `v10.1.6`）适配而来的代码，许可信息见 [`NOTICE.md`](../../NOTICE.md)。

## 2. Path B 架构（编译期代码生成）

> 自 PR [#96](https://github.com/Skymly/Observables/pull/96)（`0.1.1`）起，RestAPI 域从**运行时反射模型**迁移到**完全编译期代码生成模型**。

### 2.1 旧模型（已删除）

| 旧组件 | 职责 | 删除原因 |
|--------|------|----------|
| `IRequestBuilder` / `RequestBuilder` | 运行时反射构建请求 | 反射开销、AOT/trim 受限 |
| `RequestBuilderImplementation.cs`（1872 行） | 运行时反射核心 | 语义知识重复 |
| `RestMethodInfo.cs` / `RestMethodParameterInfo.cs` | 运行时方法元数据 | 编译期已可提取 |
| `CachedRequestBuilderImplementation.cs` | 缓存层 | 不再需要 |
| `CloseGenericMethodKey.cs` | 闭式泛型缓存键 | 不再需要 |

### 2.2 新模型（Path B）

- **Parser** 在编译期从接口方法符号提取全部 HTTP 语义（HTTP 方法、路径模板、参数绑定、序列化方式、静态头、multipart 设置等）。
- **返回类型**：HTTP 方法经域内 `RestApiReturnTypeClassifier`（`Classify` → `RestApiReturnClassification`）分类；Observable / `IObservable` 分支读 `BackendTokens.IsR3`（不并入共享 `ObservableReturnTypeParser`，见 ROADMAP C1）。
- **Emitter** 直接生成 `HttpRequestMessage` 构建代码与 `RestApiBridge` 调用，生成代码是直观的命令式方法体。
- **运行时**不再反射接口元数据；`RestApiBridge` 仅提供静态助手（路径/查询格式化、请求发送、序列化）。
- **代理注册**通过 `ModuleInitializer` + `RestService.RegisterGeneratedFactory` 自动注册（所有消费者 TFM）。ns2.0 由生成器发出 `partial ModuleInitializerAttribute` polyfill；闭式接口无 `Type.GetType` / `Activator.CreateInstance` 回退——唯一例外是**开放泛型接口**：按 `typeof(IFoo<>)` 注册的工厂在 `For<IFoo<TClosed>>()` 时经 `MakeGenericType` + `Activator.CreateInstance` 构造一次 closed impl（见 `Emitter` 开放泛型分支）。

### 2.3 收益

- 净减约 2744 行（+1432 / -4176）。
- AOT/trim 友好性显著改善（运行时无 `RequiresUnreferencedCode` 路径）。
- 生成代码可读性大幅提升（直接看到 `HttpRequestMessage` 构建，而非不透明委托调用）。

## 3. 公共面

### 3.1 HTTP 方法特性

```csharp
namespace Observables.RestAPI;

public abstract class HttpMethodAttribute(string path) : Attribute
{
    public abstract HttpMethod Method { get; }
    public virtual string Path { get; protected set; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class GetAttribute(string path) : HttpMethodAttribute(path);
// 同样：PostAttribute、PutAttribute、DeleteAttribute、PatchAttribute、HeadAttribute、OptionsAttribute
```

每个 HTTP 方法须指定一个 HTTP 方法特性。同时声明多个特性会报 OBS3009。

### 3.2 参数绑定特性

| 特性 | 目标 | 绑定种类 | 说明 |
|------|------|----------|------|
| `[Body]` / `[Body(buffered)]` / `[Body(method)]` | parameter | `Body` | 请求体；`BodySerializationMethod`（Default/Json/UrlEncoded/Serialized） |
| `[Query]` / `[Query(delimiter)]` / `[Query(Format = "…")]` / `[Query(collectionFormat)]` | parameter | `Query` | 查询参数，支持 `Prefix`、`Delimiter`、`Format`、`TreatAsString`、`CollectionFormat` |
| `[Header("name")]` | parameter | `Header` | 单个请求头 |
| `[HeaderCollection]` | parameter（`IDictionary<string,string>`） | `HeaderCollection` | 请求头集合 |
| `[Authorize("Bearer")]` | parameter | `Authorize` | 授权头 `Authorization: <scheme> <参数值>`——参数值即 token，scheme 缺省 `Bearer`；`RestApiSettings.AuthorizationHeaderValueGetter` 是无参场景的设置级钩子 |
| `[Property]` / `[Property("key")]` | parameter | `Property` | 写入 `HttpRequestMessage.Properties` / `Options` |
| `[AliasAs("name")]` | parameter / property | — | 重命名（path/query/body 字段） |
| `[Multipart("boundary")]` | method | — | 标记 multipart 方法；参数自动绑定为 `Multipart` |
| `[Headers("k: v", ...)]` | interface / method | — | 静态请求头 |
| `[QueryUriFormat(UriFormat)]` | method | — | 查询字符串转义策略 |

- `[Query]` 的命名属性覆盖构造参数。显式 `Format = null` 清除构造参数提供的格式。
- 静态请求头的名称匹配不区分大小写。方法、代理接口和适用的基接口按此顺序决定优先级。基接口按从派生到基类型的顺序应用，继承方法跳过无关的兄弟接口。同名头保留最高优先级作用域中声明的各个不同值。
- 同一方法同时声明 `[Multipart]` 和 `[Body]` 参数会报 OBS3010。运行时也会在构造请求前拒绝包含这个组合的旧生成规格。

### 3.3 入口工厂

```csharp
public static class RestService
{
    // 生成器通过 ModuleInitializer 自动注册
    public static void RegisterGeneratedFactory(Type interfaceType, Func<HttpClient, RestApiSettings?, object> factory);

    public static T For<T>(HttpClient client, RestApiSettings? settings = null);
    public static T For<T>(string hostUrl, RestApiSettings? settings = null);
    public static object For(Type interfaceType, HttpClient client, RestApiSettings? settings = null);
    public static object For(Type interfaceType, string hostUrl, RestApiSettings? settings = null);
    public static HttpMessageHandler CreateHttpMessageHandler(RestApiSettings? settings);
}
```

`For<T>` 查找 `GeneratedFactories`：先精确类型，再开放泛型定义。未命中则抛 `InvalidOperationException`（接口未生成或 `ModuleInitializer` 未运行）。`For(string hostUrl)` 会给 BaseAddress **补尾斜杠**，相对路径不再以 `/` 开头，避免丢掉 `/api/v1` 这类 base path。

### 3.4 配置（`RestApiSettings`）

```csharp
public class RestApiSettings
{
    public IHttpContentSerializer ContentSerializer { get; set; }          // 默认 SystemTextJsonContentSerializer
    public IUrlParameterFormatter UrlParameterFormatter { get; set; }
    public IUrlParameterKeyFormatter UrlParameterKeyFormatter { get; set; }
    public IFormUrlEncodedParameterFormatter FormUrlEncodedParameterFormatter { get; set; }
    public CollectionFormat CollectionFormat { get; set; }
    public bool Buffered { get; set; }
    public Func<HttpRequestMessage, CancellationToken, Task<string>>? AuthorizationHeaderValueGetter { get; set; }
    public Uri? AuthorizationHeaderOrigin { get; set; }
    public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }
    public Func<HttpResponseMessage, Task<Exception?>> ExceptionFactory { get; set; }
    public Func<HttpResponseMessage, Exception, Task<Exception?>>? DeserializationExceptionFactory { get; set; }
    public Dictionary<string, object>? HttpRequestMessageOptions { get; set; }
    public Version Version { get; set; } = HttpVersion.Version11;          // NET6+
    public HttpVersionPolicy VersionPolicy { get; set; } = RequestVersionOrLower; // NET6+
}
```

读取时机（`RestApiProtocol.Bind` 构造 `HttpRequestMessage` 时）：先灌 `HttpRequestMessageOptions`，再灌代理入口捕获的 `RestApiCallScope` 快照（见 §3.5），最后走参数绑定——优先级由低到高为「设置 < 入口快照 < `[Property]` 参数」，同键后者覆盖前者。设置是默认值，快照与 `[Property]` 是本次调用的值；未 `Push` 作用域的调用方拿到的快照是 `null`，Bind 跳过中间层，行为与 #510 引入 `HttpRequestMessageOptions` 时完全一致。`Version` / `VersionPolicy` 直接赋给请求上的同名属性（NET6+）。`UrlParameterKeyFormatter` 在 `AddQueryParameter` 里格式化 query 键，`[Query(Prefix)]` 的前缀按原样保留，只格式化键本身；并且**只格式化推断出来的键**——写了 `[AliasAs("…")]` 就是最终线上名字，跳过 formatter（与 Refit 一致）。这一位由生成器在编译期定下：`QueryOptions.WithExplicitName()` 只在有 alias 时发射，运行时靠 `QueryOptions.NameIsExplicit` 读它。之所以是方法而不是再加一个构造重载，是因为已 ship 的那个带可选参数的构造函数必须保持「公共重载里参数最多」（RS0027）。

公开的 `RestApiBridge.AddQueryParameter` 重载签名不动，行为也不变（照旧格式化每个键）；带 `nameIsExplicit` 的是 internal 重载，只有 `Bind` 走。

仍未对齐 Refit 的部分：Refit 还会把 query object 展开后对每个属性名调这个 formatter，Observables 目前不展开 query object（非集合的对象整体 `FormatQueryValue` 成单个值）。

带凭据的显式 `Authorization` 请求头优先保留。缺少此头或仅指定授权方案时，处理器可调用 `AuthorizationHeaderValueGetter` 填入凭据。将 `AuthorizationHeaderOrigin` 设为绝对 HTTP(S) URI 后，处理器会在调用回调前和回调返回后校验请求的协议、IDN 主机名与有效端口。默认值 `null` 保留原有行为。`For(hostUrl)` 和 HttpClientFactory 扩展使用 `RestService.CreateHttpMessageHandler` 安装该处理器。调用方自行创建 `HttpClient` 时，也可用此方法配置其处理器.

### 3.5 每次调用选项（`RestApiCallScope`）

```csharp
public static class RestApiCallScope
{
    // 为作用域存续期间进入的代理方法安装请求选项；返回的 IDisposable 恢复上一作用域
    public static IDisposable Push(IReadOnlyDictionary<string, object?> options);

    // 返回当前 async flow 上的不可变快照；无作用域时为 null
    public static IReadOnlyDictionary<string, object?>? Capture();
}
```

- **存储**：`AsyncLocal` 节点链，按 async flow 隔离——不同 flow 上的并发调用互不可见对方快照。
- **`Push`** 立即把「外层继承 + 内层覆盖」合并冻结为 `ReadOnlyDictionary`（`StringComparer.Ordinal`）：字典结构从此固定，之后改 `options` 字典本身无影响；但选项**值**按引用共享，改可变值仍影响持有该快照的 Bind。`options` 为 `null` 抛 `ArgumentNullException`，键为 `null` 抛 `ArgumentException`。
- **Dispose**：按 `Push` 逆序（LIFO）。乱序 Dispose 不顶替当前内层作用域——节点直接从链上摘除，之后的 pop 也不会复活它；重复 Dispose 幂等。
- **入口快照**：生成代理在**方法入口**调 `Capture()`——Task 变体在第一行，同步 `void`/非 await 变体在 `Task.Run` 之前，Observable 变体在 `FromAsync` 之外——快照作为 `callOptions` 传给 `RestApiBridge.SendAsync` / `SendVoidAsync`。`RestApiProtocol.Bind` **不得**再读 ambient 作用域：Bind/发送可能晚于作用域 Dispose（延迟订阅、线程池发送），回读会拿到与入口时不一致的值。
- **优先级**（低→高）：`RestApiSettings.HttpRequestMessageOptions` → 入口快照 → 同键 `[Property]` 参数。快照为 `null`（未 `Push`）时 Bind 跳过中间层，等价于 #510 的二层行为。
- **TFM 存储与 null 语义**：ns2.0 写 `HttpRequestMessage.Properties`，net6+ 写 `Options`（生成代码内嵌 `#if` 由消费者编译器选择，见 §4.5）。值为 `null` 表示清除该键的设置值——net6+ 存的是 null，`Options.TryGetValue` 不显露已存 null；ns2.0 直接 `Remove` 该键，使 `ContainsKey` / `TryGetValue` 与清除语义一致。注意 `HttpRequestMessageOptions` 的值类型是 `object`（非空），只有快照与 `[Property]` 参数能表达「清除」。
- **可见性**：带 `callOptions` 的 `SendAsync` / `SendVoidAsync` 重载标 `EditorBrowsableState.Never`——public 仅因生成代码运行在消费者程序集；无 `callOptions` 的同名重载转发 `callOptions: null`。

### 3.6 响应与异常

| 类型 | 用途 |
|------|------|
| `IApiResponse<T>` / `ApiResponse<T>` | 包装响应，提供 `Content`、`Headers`、`StatusCode`、`Error` |
| `IApiResponse` | 包装响应元数据和错误，跳过响应体反序列化 |
| `ApiExceptionBase` | 所有 RestAPI 异常基类（含 `RequestMessage`、`HttpMethod`、`Uri`） |
| `ApiRequestException` | 请求阶段失败（发送前） |
| `ApiException` | 响应阶段失败（含 `StatusCode`、`Content`、`Headers`） |
| `ValidationApiException` | `ApiException` 子类，用于校验失败场景 |

`Task` 和 `ValueTask` 走 `SendVoidAsync`。此路径同样按绑定结果决定是否缓存请求体、使用 `ResponseHeadersRead`，并将传输失败包装为 `ApiRequestException`。调用方取消时传播取消异常。

成功构造的响应包装器持有并负责释放请求与响应消息，调用方应在用完包装器后调用 `Dispose()`。`IsReceived` 只表示收到响应，`Content` 仍可为空。可通过 `HasRequestError` 和 `HasResponseError` 检查 `Error` 的类型。

## 4. 生成映射

### 4.1 接口 → 代理类

输入：

```csharp
public interface IUserApi
{
    [Get("/users/{id}")]
    Task<User> GetUser(int id);

    [Get("/users/{id}")]
    Observable<User> GetUserObservable(int id);   // R3 路径
}
```

生成（Task 变体，摘自 Verify 快照，`global::` 前缀已省略展示）：

```csharp
namespace Observables.RestAPI.Implementation;

internal static partial class Generated
{
    partial class IUserApi : global::IUserApi
    {
        public HttpClient Client { get; }
        readonly RestApiSettings _settings;

        public IUserApi(HttpClient client, RestApiSettings? settings)
        {
            Client = client;
            _settings = settings ?? new RestApiSettings();
        }

        // 每方法一个静态 MethodSpec：HTTP 方法 + 路径模板 + 参数绑定槽
        static readonly RestApiBridge.MethodSpec ______spec0 =
            new("GET", "/users/{id}", new RestApiBridge.Binding[] { new(RestApiBridge.SlotKind.Path, 0, "id") });

        public async Task<global::User> GetUser(int @id)
        {
            var ______scope = RestApiCallScope.Capture();          // 每次调用的选项快照
            var ______ct = CancellationToken.None;
            return await RestApiBridge.SendAsync<global::User, global::User>(
                Client, _settings, in ______spec0, ______scope, ______ct, new object[] { @id })
                .ConfigureAwait(false);
        }
    }
}
```

生成（Observable 变体，R3）：

```csharp
public global::R3.Observable<global::User> GetUserObservable(int @id)
{
    var ______scope = RestApiCallScope.Capture();       // 入口快照，在 FromAsync 之外
    return global::R3.Observable.FromAsync(async ct =>
    {
        return await RestApiBridge.SendAsync<global::User, global::User>(
            Client, _settings, in ______spec0, ______scope, ct, new object[] { @id })
            .ConfigureAwait(false);
    });
}
```

Reactive 包对应为 `Observables.RestAPI.Reactive.SystemReactiveObservableAdapter.FromAsync(async ct => ...)`。发送路径统一走 `RestApiBridge.SendAsync(client, settings, in MethodSpec, callOptions, ct, params args)`：`RestApiProtocol.Bind` 按 `MethodSpec` 的 `Binding[]` 槽把 `args` 组装成 `HttpRequestMessage`；`RestApiCallScope.Capture()` 在代理方法入口把当前 async flow 的请求选项固化为 `callOptions`（见 §3.5），`Bind` 只消费该快照、不回读 ambient 作用域。

### 4.2 ModuleInitializer 注册

```csharp
internal static partial class Generated
{
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage(...)]
#endif
    [ModuleInitializer]
    public static void Initialize()
    {
        RestService.RegisterGeneratedFactory(
            typeof(global::IUserApi),
            static (client, settings) => new Generated.IUserApi(client, settings));
    }
}

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed partial class ModuleInitializerAttribute : Attribute { }
}
#endif
```

### 4.3 路径模板

编译期由 `RestApiPathTemplate` 校验路径模板与参数的对应关系（占位符必须有绑定、未标特性参数可自动补位、多余占位符会被剔除并给出建议路径）；发射期把模板原样写进每方法静态 `RestApiBridge.MethodSpec.PathTemplate`，参数槽由 `Binding[]`（`SlotKind.Path`/`Query`/…）描述。运行时 `RestApiProtocol.Bind` 按 spec 做替换与格式化：path 段走 `FormatPathParameter`（URL 转义），query 走 `AddQueryParameter` + `UrlParameterKeyFormatter`。生成代码本身不再做路径字符串拼接。

### 4.4 参数绑定分类（`ParameterKind`）

```csharp
internal enum ParameterKind : byte
{
    None, Path, Query, Body, Header, HeaderCollection, Authorize, Property, Multipart, CancellationToken,
}
```

Parser 按特性与路径模板占位符匹配确定每个参数的 `ParameterKind`，Emitter 据此生成对应的请求构建代码。

### 4.5 netstandard2.0 兼容性

工厂注册不依赖 `NET5_0_OR_GREATER`：生成 `Initialize` + `ModuleInitializerAttribute` polyfill，由 C# 9+ 编译器在模块加载时调用。

`Property` 参数绑定使用 `#if NET6_0_OR_GREATER` 条件编译：.NET 6+ 用 `HttpRequestMessage.Options.Set(new HttpRequestOptionsKey<T>(...))`，旧框架用 `HttpRequestMessage.Properties[...]`。生成代码内嵌 `#if`，由消费者编译器选择路径。

## 5. 运行时桥接（`RestApiBridge`）

```csharp
public static class RestApiBridge
{
    // 生成代码使用的发送入口（in MethodSpec + 可选 callOptions + params args）
    public static Task<T?> SendAsync<T, TBody>(HttpClient client, RestApiSettings settings, in MethodSpec spec, CancellationToken ct, params object?[] args);
    public static Task<T?> SendAsync<T, TBody>(HttpClient client, RestApiSettings settings, in MethodSpec spec, IReadOnlyDictionary<string, object?>? callOptions, CancellationToken ct, params object?[] args);
    public static Task SendVoidAsync(HttpClient client, RestApiSettings settings, in MethodSpec spec, CancellationToken ct, params object?[] args);
    public static Task SendVoidAsync(HttpClient client, RestApiSettings settings, in MethodSpec spec, IReadOnlyDictionary<string, object?>? callOptions, CancellationToken ct, params object?[] args);

    // 旧形态重载（接收预构建的 HttpRequestMessage，RestApiProtocol.Bind 内部回传使用）
    public static Task<T?> SendAsync<T, TBody>(HttpClient client, HttpRequestMessage request, RestApiSettings settings, bool bodyBuffered, CancellationToken ct);
    public static Task<T?> SendAsync<T, TBody>(HttpClient client, HttpRequestMessage request, RestApiSettings settings, bool isApiResponse, bool bodyBuffered, CancellationToken ct);  // isApiResponse 已忽略
    public static Task SendVoidAsync(HttpClient client, HttpRequestMessage request, RestApiSettings settings, CancellationToken ct);

    public static string FormatPathParameter(object? value, RestApiSettings settings);
    public static string? FormatQueryValue(object? value, RestApiSettings settings);
    public static void AddQueryParameter(List<KeyValuePair<string, string?>> queryParams, string key, object? value, RestApiSettings settings, string? prefix = null, string delimiter = ".", string? format = null, bool treatAsString = false, int collectionFormat = 0, bool isCollectionFormatSpecified = false);
    public static string? BuildQueryString(List<KeyValuePair<string, string?>>? queryParams, UriFormat uriFormat = UriFormat.UriEscaped);
    public static string BuildRelativePath(string path, List<KeyValuePair<string, string?>>? queryParams, UriFormat uriFormat = UriFormat.UriEscaped);
    public static HttpContent SerializeBody<T>(T value, RestApiSettings settings, int bodySerializationMethod = 0);
    public static HttpContent CreateFormUrlEncodedContent(object value, RestApiSettings settings);
    public static void AddMultipartItem(MultipartFormDataContent multiPartContent, string fileName, string parameterName, object? itemValue, RestApiSettings settings);
}
```

生成代码只调用这些静态方法；复杂逻辑（序列化、异常处理、响应反序列化）集中在运行时，便于维护与测试。

## 6. 双后端条件编译

| 生成器项目 | `DefineConstants` | 返回类型分类（`RestApiReturnTypeClassifier`） | Emitter 行为 |
|------------|-------------------|-----------------------------------------------|--------------|
| `RestAPI.R3.SourceGenerators` | `RESTAPI_R3`（+ `OBSERVABLES_R3` via R3.props） | `Observable<T>` → `R3Observable`。`IObservable<T>` → OBS3003 | `#if RESTAPI_R3` → `R3.Observable.FromAsync(...)` |
| `RestAPI.Reactive.SourceGenerators` | `RESTAPI_REACTIVE` | `IObservable<T>` → `SystemReactiveObservable`（缺少 Reactive 桥时为 OBS3005）。R3 `Observable<T>` → OBS3003 | `#else` → `SystemReactiveObservableAdapter.FromAsync(...)` |

两生成器共享同一份 shproj：返回类型分类走 `BackendTokens.IsR3`；Emitter 仍用 `#if RESTAPI_R3` 切换发射文本。

## 7. 诊断（OBS3xxx）

归属：`Observables.RestAPI/Observables.RestAPI.SourceGenerators.Shared/DiagnosticDescriptors.cs`。

| ID | 严重性 | 触发 |
|----|--------|------|
| OBS3001 | Warning | 接口方法缺少 HTTP 方法特性或路径非常量 |
| OBS3002 | Error | 未引用消费者包 `Observables.RestAPI.R3` / `Observables.RestAPI.Reactive`（按生成器后端） |
| OBS3003 | Error | 不支持的返回类型 |
| OBS3004 | Error | 路径模板与参数不匹配（占位符无对应参数或反之） |
| OBS3005 | Error | `IObservable<T>` 返回但缺少 Reactive 桥接适配器（**Reactive 生成器侧**；R3 侧同情形报 OBS3003） |
| OBS3006 | Error | 源生成器内部错误 |
| OBS3007 | Warning | 空 `[RestApi]` 接口（未声明任何成员，`Observables.Analyzers`） |
| OBS3008 | Error | 同一方法声明了多个 `[Body]` 参数 |
| OBS3009 | Error | 同一方法声明了多个 HTTP 方法特性 |
| OBS3010 | Error | 同一方法声明了 `[Multipart]` 和 `[Body]` 参数 |

域生成器诊断见 [已发布登记](../../Observables.RestAPI/Observables.RestAPI.SourceGenerators.Shared/AnalyzerReleases.Shipped.md)与[待发布登记](../../Observables.RestAPI/Observables.RestAPI.SourceGenerators.Shared/AnalyzerReleases.Unshipped.md)。OBS3007 由 [共享分析器](../../Observables.Shared/Observables.Analyzers/AnalyzerReleases.Shipped.md)维护。

## 8. HttpClientFactory 扩展

`Observables.RestAPI.HttpClientFactory`（独立项目，不捆绑生成器）提供 DI 集成：

```csharp
public static class HttpClientFactoryExtensions
{
    public static IHttpClientBuilder AddRestApiClient<T>(
        this IServiceCollection services,
        Action<HttpClient>? configureClient = null,
        RestApiSettings? settings = null) where T : class;
}
```

内部通过 `IHttpClientFactory` 创建 `HttpClient`，调用 `RestService.For<T>()` 创建代理，注册为 typed client。通过 `RestService.CreateHttpMessageHandler` 应用自定义处理器工厂、凭据回调和授权来源限制。

## 9. 项目组成

```
Observables.RestAPI/
├── Observables.RestAPI/                              # 运行时（Attributes、RestService、RestApiBridge、RestApiSettings、ApiResponse）
├── Observables.RestAPI.Reactive/                     # System.Reactive 桥接（SystemReactiveObservableAdapter）
├── Observables.RestAPI.SourceGenerators.Shared/      # shproj（Parser、Emitter、Models、诊断）
├── Observables.RestAPI.R3.SourceGenerators/          # R3 生成器（RESTAPI_R3）
├── Observables.RestAPI.Reactive.SourceGenerators/    # Reactive 生成器（RESTAPI_REACTIVE）
├── Observables.RestAPI.HttpClientFactory/            # DI 扩展（独立包，不捆绑生成器）
├── Observables.RestAPI.Package/                      # Traversal 根，产出 2 个 NuGet 包
│   ├── Observables.RestAPI.R3.csproj                 # PackageId = Observables.RestAPI.R3
│   └── Observables.RestAPI.Reactive.Pack.csproj      # PackageId = Observables.RestAPI.Reactive
├── Observables.RestAPI.Tests/                        # 运行时测试
├── Observables.RestAPI.Reactive.Tests/               # Reactive 运行时测试
├── Observables.RestAPI.GeneratorTests/               # 生成器快照测试（VerifyXunit；R3 + Reactive 双 harness）
└── Observables.RestAPI.HttpClientFactory.Tests/      # DI 扩展测试
```

## 10. 关键设计决策

| 决策 | 理由 |
|------|------|
| **Path B 编译期生成** | 消除运行时反射，AOT/trim 友好，生成代码可读 |
| **`RestApiBridge` 静态助手** | 复杂逻辑集中在运行时，生成代码只做「编译期已知信息的拼接」 |
| **`ModuleInitializer` 注册** | 所有 TFM 自动注册（ns2.0 polyfill）；无反射回退 |
| **`BodySerializationMethod` 镜像枚举** | 生成器（netstandard2.0）不引用运行时项目，用 internal 镜像枚举 + `int` 传递 |
| **`#if NET6_0_OR_GREATER` 嵌入生成代码** | `HttpRequestMessage.Options` vs `Properties` 由消费者编译器选择 |
| **`BackendTokens.IsR3` + `#if RESTAPI_R3`（Emitter）** | 返回类型分类与 IO 域共用 compile-time backend seam；Emitter 仍用域级 `#if` 切换发射文本 |
| **每方法静态 `MethodSpec` + `in` 传递** | 请求形状（HTTP 方法、路径模板、`Binding[]` 槽、flags）编译期定死为静态字段，运行时不重建字符串/元数据 |
| **入口快照 `RestApiCallScope`（不加方法参数）** | 每次调用选项经 `AsyncLocal` 作用域传入、入口 `Capture()` 冻结为 `callOptions`；`Bind`/发送可能晚于作用域 Dispose（`Task.Run` 池线程、延迟订阅），回读 ambient 会拿到不一致的值；快照优先级介于设置与 `[Property]` 之间，未 `Push` 时为零成本跳过 |
| **`IApiResponse` / `IApiResponse<T>`** | 调用方可通过包装器读取状态、响应头和错误。有类型的包装器还提供反序列化内容 |
| **HttpClientFactory 独立包** | DI 集成可选，不强制依赖 `Microsoft.Extensions.Http` |

## 11. 后续（v1 之外）

- 在 .NET 8 或更高版本上，可用 `new RestApiSettings(SystemTextJsonContentSerializer.FromContext(MyJsonContext.Default))`，让生成代理从该上下文解析请求和响应的 `JsonTypeInfo`。上下文缺少 DTO 时会明确报错。HTTP 验证错误的 ProblemDetails 使用库内生成的元数据。Native AOT 的 JSON 路径支持闭合代理，URL 编码表单可用字符串或字典。需要自定义枚举线值时，通过 settings 配置格式化器。
- `DynamicallyAccessedMemberTypes` 精细化（Path B 已显著收敛，但 `RestService.For` 各重载仍带 `PublicMethods | PublicProperties`，比 `TryGetGeneratedFactory` 查表实际需要的宽）。
- 流式响应（`IAsyncEnumerable<T>`）支持。
- 多部分上传的 `Stream` 优化（当前 `AddMultipartItem` 对 `Stream` 走 `StreamContent`）。
