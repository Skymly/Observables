# RestAPI 域 — 开发设计文档

> 状态：**已实现**（`main`，Path B 编译期代码生成）；NuGet `Observables.RestAPI.R3` / `Observables.RestAPI.Reactive` 已发 nuget.org（`0.1.1`，16 包之一）。实现细节以代码为准。
> 命名、打包、诊断分段等约定以仓库根 [`AGENTS.md`](../../AGENTS.md) 为权威，本文在其框架内细化 RestAPI 域。

## 1. 目标与定位

将 **HTTP REST 边界**通过 Roslyn 源生成器桥接为反应式流：声明式接口（Refit 风格特性）→ 编译期生成 `HttpClient` 代理实现 → 运行时直接调用，无反射。

| 返回类型 | 后端 | 语义 |
|----------|------|------|
| `Task<T>` / `ValueTask<T>` / `Task` | 通用 | 一次性请求/响应 |
| `Observable<T>` | R3 包 | 单次响应冷流（`Observable.FromAsync`） |
| `IObservable<T>` | Reactive 包 | 单次响应冷流（`SystemReactiveObservableAdapter.FromAsync`） |
| `IApiResponse<T>` | 通用 | 包装响应（含头、状态码、错误，不抛异常） |

RestAPI 域的运行时部分包含由 [Refit](https://github.com/reactiveui/refit)（Apache 2.0）适配而来的代码，许可信息见 [`NOTICE.md`](../../NOTICE.md)。

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
- **Emitter** 直接生成 `HttpRequestMessage` 构建代码与 `RestApiBridge` 调用，生成代码是直观的命令式方法体。
- **运行时**不再反射接口元数据；`RestApiBridge` 仅提供静态助手（路径/查询格式化、请求发送、序列化）。
- **代理注册**通过 `ModuleInitializer` + `RestService.RegisterGeneratedFactory` 自动注册（.NET 5+）；旧路径 `Type.GetType` + `Activator.CreateInstance` 保留为回退。

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

### 3.2 参数绑定特性

| 特性 | 目标 | 绑定种类 | 说明 |
|------|------|----------|------|
| `[Body]` / `[Body(buffered)]` / `[Body(method)]` | parameter | `Body` | 请求体；`BodySerializationMethod`（Default/Json/UrlEncoded/Serialized） |
| `[Query]` / `[Query(delimiter)]` / `[Query(format)]` / `[Query(collectionFormat)]` | parameter | `Query` | 查询参数；支持 `Prefix`、`Delimiter`、`Format`、`TreatAsString`、`CollectionFormat` |
| `[Header("name")]` | parameter | `Header` | 单个请求头 |
| `[HeaderCollection]` | parameter（`IDictionary<string,string>`） | `HeaderCollection` | 请求头集合 |
| `[Authorize("Bearer")]` | parameter | `Authorize` | 授权头（由 `RestApiSettings.AuthorizationHeaderValueGetter` 取值） |
| `[Property]` / `[Property("key")]` | parameter | `Property` | 写入 `HttpRequestMessage.Properties` / `Options` |
| `[AliasAs("name")]` | parameter / property | — | 重命名（path/query/body 字段） |
| `[Multipart("boundary")]` | method | — | 标记 multipart 方法；参数自动绑定为 `Multipart` |
| `[Headers("k: v", ...)]` | interface / method | — | 静态请求头 |
| `[QueryUriFormat(UriFormat)]` | method | — | 查询字符串转义策略 |

### 3.3 入口工厂

```csharp
public static class RestService
{
    // 生成器通过 ModuleInitializer 自动注册
    public static void RegisterGeneratedFactory(Type refitInterfaceType, Func<HttpClient, RestApiSettings?, object> factory);

    public static T For<T>(HttpClient client, RestApiSettings? settings = null);
    public static T For<T>(string hostUrl, RestApiSettings? settings = null);
    public static object For(Type refitInterfaceType, HttpClient client, RestApiSettings? settings = null);
    public static object For(Type refitInterfaceType, string hostUrl, RestApiSettings? settings = null);
}
```

`For<T>` 优先查找 `GeneratedFactories` 字典；未命中则回退到 `Type.GetType` + `Activator.CreateInstance`（非 .NET 5+ 场景）。

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
    public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }
    public Func<HttpResponseMessage, Task<Exception?>> ExceptionFactory { get; set; }
    public Func<HttpResponseMessage, Exception, Task<Exception?>>? DeserializationExceptionFactory { get; set; }
    public Dictionary<string, object>? HttpRequestMessageOptions { get; set; }
    public Version Version { get; set; } = HttpVersion.Version11;          // NET6+
    public HttpVersionPolicy VersionPolicy { get; set; } = RequestVersionOrLower; // NET6+
}
```

### 3.5 响应与异常

| 类型 | 用途 |
|------|------|
| `IApiResponse<T>` / `ApiResponse<T>` | 包装响应（含 `Content`、`Headers`、`StatusCode`、`Error`），不抛异常 |
| `ApiExceptionBase` | 所有 RestAPI 异常基类（含 `RequestMessage`、`HttpMethod`、`Uri`） |
| `ApiRequestException` | 请求阶段失败（发送前） |
| `ApiException` | 响应阶段失败（含 `StatusCode`、`Content`、`Headers`） |
| `ValidationApiException` | `ApiException` 子类，用于校验失败场景 |

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

生成（Task 变体，简化）：

```csharp
namespace Observables.RestAPI.Implementation
{
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

            public async Task<global::User> GetUser(int id)
            {
                var ct = CancellationToken.None;
                if (Client.BaseAddress == null)
                    throw new InvalidOperationException("BaseAddress must be set on the HttpClient instance");
                var path = "/users/" + RestApiBridge.FormatPathParameter(id, _settings);
                var request = new HttpRequestMessage { Method = HttpMethod.Get };
                request.RequestUri = new Uri(path, UriKind.Relative);
                return await RestApiBridge.SendAsync<User, User>(Client, request, _settings, false, false, ct)
                    .ConfigureAwait(false);
            }
        }
    }
}
```

生成（Observable 变体，R3）：

```csharp
public global::R3.Observable<global::User> GetUserObservable(int id)
{
    return global::R3.Observable.FromAsync(async ct =>
    {
        // ... 同样的请求构建 ...
        return await RestApiBridge.SendAsync<User, User>(Client, request, _settings, false, false, ct)
            .ConfigureAwait(false);
    });
}
```

Reactive 包对应为 `Observables.RestAPI.Reactive.SystemReactiveObservableAdapter.FromAsync(async ct => ...)`。

### 4.2 ModuleInitializer 注册

```csharp
internal static partial class Generated
{
#if NET5_0_OR_GREATER
    [ModuleInitializer]
    public static void Initialize()
    {
        RestService.RegisterGeneratedFactory(
            typeof(global::IUserApi),
            static (client, settings) => new Generated.IUserApi(client, settings));
    }
#endif
}
```

### 4.3 路径模板

`PathFragmentModel`（readonly record struct）将路径拆为常量段与参数引用段：

```csharp
internal readonly record struct PathFragmentModel(string? ConstantValue, int ParameterIndex, bool IsConstant);
```

Emitter 将 `PathFragmentModel[]` 拼为字符串拼接表达式：`"/users/" + RestApiBridge.FormatPathParameter(id, _settings)`。常量段编译期确定，参数段运行时格式化并 URL 转义。

### 4.4 参数绑定分类（`ParameterKind`）

```csharp
internal enum ParameterKind : byte
{
    None, Path, Query, Body, Header, HeaderCollection, Authorize, Property, Multipart, CancellationToken,
}
```

Parser 按特性与路径模板占位符匹配确定每个参数的 `ParameterKind`，Emitter 据此生成对应的请求构建代码。

### 4.5 netstandard2.0 兼容性

`Property` 参数绑定使用 `#if NET6_0_OR_GREATER` 条件编译：.NET 6+ 用 `HttpRequestMessage.Options.Set(new HttpRequestOptionsKey<T>(...))`，旧框架用 `HttpRequestMessage.Properties[...]`。生成代码内嵌 `#if`，由消费者编译器选择路径。

## 5. 运行时桥接（`RestApiBridge`）

```csharp
public static class RestApiBridge
{
    public static string FormatPathParameter(object? value, RestApiSettings settings);
    public static string? FormatQueryValue(object? value, RestApiSettings settings);
    public static Task<T?> SendAsync<T, TBody>(HttpClient client, HttpRequestMessage request, RestApiSettings settings, bool isApiResponse, bool bodyBuffered, CancellationToken ct);
    public static Task SendVoidAsync(HttpClient client, HttpRequestMessage request, RestApiSettings settings, CancellationToken ct);
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

R3 与 System.Reactive 的切换通过 `#if` 编译指令实现：

| 生成器项目 | `DefineConstants` | Parser 行为 | Emitter 行为 |
|------------|-------------------|-------------|--------------|
| `RestAPI.R3.SourceGenerators` | `RESTAPI_R3` | `Observable<T>` → `R3Observable`；`IObservable<T>` → OBS3005 | `R3.Observable.FromAsync(...)` |
| `RestAPI.Reactive.SourceGenerators` | `RESTAPI_SYSTEM_REACTIVE` | `IObservable<T>` → `SystemReactiveObservable`；`Observable<T>` → OBS3003 | `SystemReactiveObservableAdapter.FromAsync(...)` |

两生成器共享同一份 `Parser.cs` / `Emitter.cs`（shproj），通过 `#if` 切换后端特定逻辑。

## 7. 诊断（OBS3xxx）

归属：`Observables.RestAPI/Observables.RestAPI.SourceGenerators.Shared/DiagnosticDescriptors.cs`。

| ID | 严重性 | 触发 |
|----|--------|------|
| OBS3001 | Warning | 接口方法缺少 HTTP 方法特性或路径非常量 |
| OBS3002 | Error | 未引用 `Observables.RestAPI` 运行时 |
| OBS3003 | Error | 不支持的返回类型 |
| OBS3004 | Error | 路径模板与参数不匹配（占位符无对应参数或反之） |
| OBS3005 | Error | `IObservable<T>` 返回但未引用 `Observables.RestAPI.Reactive`（R3 生成器侧） |
| OBS3007 | Warning | 空 RestAPI 代理接口（无有效 HTTP 方法成员，`Observables.Analyzers`） |

Release 跟踪：`Observables.RestAPI.SourceGenerators.Shared/AnalyzerReleases.Shipped.md`（OBS3001–3005 v1.0 已发）；`Observables.Analyzers/AnalyzerReleases.Unshipped.md`（OBS3007 待下次发版迁入 Shipped）。

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

内部通过 `IHttpClientFactory` 创建 `HttpClient`，调用 `RestService.For<T>()` 创建代理，注册为 typed client。支持 `AuthorizationHeaderValueGetter` 与 `HttpMessageHandlerFactory` 自动包装。

## 9. 项目组成

```
Observables.RestAPI/
├── Observables.RestAPI/                              # 运行时（Attributes、RestService、RestApiBridge、RestApiSettings、ApiResponse）
├── Observables.RestAPI.Reactive/                     # System.Reactive 桥接（SystemReactiveObservableAdapter）
├── Observables.RestAPI.SourceGenerators.Shared/      # shproj（Parser、Emitter、Models、诊断）
├── Observables.RestAPI.R3.SourceGenerators/          # R3 生成器（RESTAPI_R3）
├── Observables.RestAPI.Reactive.SourceGenerators/    # Reactive 生成器（RESTAPI_SYSTEM_REACTIVE）
├── Observables.RestAPI.HttpClientFactory/            # DI 扩展（独立包，不捆绑生成器）
├── Observables.RestAPI.Package/                      # Traversal 根，产出 2 个 NuGet 包
│   ├── Observables.RestAPI.R3.csproj                 # PackageId = Observables.RestAPI.R3
│   └── Observables.RestAPI.Reactive.Pack.csproj      # PackageId = Observables.RestAPI.Reactive
├── Observables.RestAPI.Tests/                        # 运行时测试
├── Observables.RestAPI.Reactive.Tests/               # Reactive 运行时测试
├── Observables.RestAPI.GeneratorTests/               # 生成器快照测试（VerifyXunit）
└── Observables.RestAPI.HttpClientFactory.Tests/      # DI 扩展测试
```

## 10. 关键设计决策

| 决策 | 理由 |
|------|------|
| **Path B 编译期生成** | 消除运行时反射，AOT/trim 友好，生成代码可读 |
| **`RestApiBridge` 静态助手** | 复杂逻辑集中在运行时，生成代码只做「编译期已知信息的拼接」 |
| **`ModuleInitializer` 注册** | .NET 5+ 自动注册，AOT 友好；保留反射回退兼容旧框架 |
| **`BodySerializationMethod` 镜像枚举** | 生成器（netstandard2.0）不引用运行时项目，用 internal 镜像枚举 + `int` 传递 |
| **`#if NET6_0_OR_GREATER` 嵌入生成代码** | `HttpRequestMessage.Options` vs `Properties` 由消费者编译器选择 |
| **`#if RESTAPI_R3` / `RESTAPI_SYSTEM_REACTIVE`** | shproj 共享一份 Parser/Emitter，编译期切换后端 |
| **`PathFragmentModel` readonly record struct** | 路径模板编译期拆分，常量段直接拼接，参数段运行时格式化 |
| **`IApiResponse<T>` 可选不抛异常** | 需要检查状态码/头的场景用 `IApiResponse<T>`；需要抛异常用 `Task<T>` |
| **HttpClientFactory 独立包** | DI 集成可选，不强制依赖 `Microsoft.Extensions.Http` |

## 11. 后续（v1 之外）

- `JsonSerializerContext` source-gen 进一步改善 AOT/trim（当前 `SystemTextJsonContentSerializer` 仍用反射式序列化）。
- `DynamicallyAccessedMemberTypes` 精细化（Path B 已显著收敛，但 `RestService.For` 回退路径仍用 `All`）。
- 流式响应（`IAsyncEnumerable<T>`）支持。
- 多部分上传的 `Stream` 优化（当前 `AddMultipartItem` 对 `Stream` 走 `StreamContent`）。
