# shproj 跨域重复审查 — 重构计划

## 背景

7 域 `*.SourceGenerators.Shared` 项目存在重复代码。经调查，子代理报告的重复程度有夸大，实际可安全提取的仅 2 项。

## 架构现状

- **全局共享** (`Observables.SourceGenerators.Shared/`)：无 `.shproj`，通过 `Observables.SourceGenerators.SharedSource.props` 以 `<Compile Include>` + `<Link>` 方式链接编译到每个生成器。`<Using Include="Observables.SourceGenerators.Shared" />` 已全局导入。
- **域共享** (`Observables.<Domain>.SourceGenerators.Shared/`)：用 `.shproj`/`.projitems`，由域内 R3 和 Reactive 生成器分别 Import。

## 真正可提取的重复（仅 2 项）

| 重复项 | 副本数 | 每份行数 | 可提取性 | 原因 |
|--------|--------|----------|----------|------|
| `Nullability` enum | 7 | 6 行 | 高 | 100% 相同，内嵌在各域 `*InterfaceModel.cs` |
| `ReportDiagnostics` 方法 | 7 | 14 行 | 高 | 100% 相同（RestAPI 另有一个额外重载，保留在域内） |

## 不可提取（子代理报告夸大的部分）

| 项目 | 子代理建议 | 实际情况 |
|------|-----------|----------|
| `ContextGenerationModel` | 提取泛型版 | 每域仅 1 行 record，提取后省 6 行，但增加泛型复杂度，不值得 |
| `EmitSource` 方法 | 泛型+委托 | 调用域特定 `Emitter.EmitInterface()`，无法 static lambda 捕获，提取会丢失 `static` 安全保证 |
| `DiagnosticDescriptors` | 模板化 | ID 和消息不同，模板化增加抽象层但无实际收益 |
| `BoundaryKind` 枚举 | 泛型基类 | 协议特定值，无法共享 |
| `Parser.cs` / `Emitter.cs` | — | 域特定逻辑，不可共享 |

## Implementation Steps

### Phase 1: 提取 `Nullability` enum

1. **创建** `Observables.Shared/Observables.SourceGenerators.Shared/Nullability.cs`
   - namespace `Observables.SourceGenerators.Shared`
   - `enum Nullability : byte { Enabled, Disabled, None }`

2. **注册** 到 `Observables.SourceGenerators.SharedSource.props` 添加 `<Compile Include>` + `<Link>Shared/Nullability.cs</Link>`

3. **删除** 7 个域 `*InterfaceModel.cs` 中的 `Nullability` enum 定义（`using` 已由 props 全局导入）

### Phase 2: 提取 `ReportDiagnostics` 扩展方法

4. **创建** `Observables.Shared/Observables.SourceGenerators.Shared/IncrementalValuesProviderExtensions.cs`
   - namespace `Observables.SourceGenerators.Shared`
   - 仅含 `ReportDiagnostics(this IncrementalGeneratorInitializationContext, IncrementalValueProvider<ImmutableEquatableArray<Diagnostic>>)` — 7 域完全相同的版本
   - 包裹 `#if ROSLYN_4`

5. **注册** 到 `SharedSource.props`

6. **从 7 个域的 `IncrementalValuesProviderExtensions.cs` 中删除 `ReportDiagnostics` 方法**
   - RestAPI 保留其额外的单条 `ReportDiagnostics(IncrementalValuesProvider<Diagnostic>)` 重载
   - 6 个简化域仅保留 `EmitSource` 方法

### Phase 3: 验证

7. `dotnet build Observables.slnx --configuration Release` — 0 error, 0 warning
8. `dotnet test` 全部生成器测试（RestAPI GeneratorTests + 7 域 R3/Reactive SourceGenerators.Tests）
9. `dotnet publish` TrimTests — ILLink 无警告
10. `sync-doc-loc.ps1 -AllDomains` — 14 项目 parity 通过

## Files to Modify

- `Observables.Shared/Observables.SourceGenerators.Shared/Nullability.cs` — 新建
- `Observables.Shared/Observables.SourceGenerators.Shared/IncrementalValuesProviderExtensions.cs` — 新建
- `Observables.SourceGenerators.SharedSource.props` — 添加 2 个 Compile Include
- 7 × `*InterfaceModel.cs` — 删除内嵌 Nullability enum
- 7 × `IncrementalValuesProviderExtensions.cs` — 删除 ReportDiagnostics 方法（RestAPI 保留额外重载）

## Verification

- [ ] `dotnet build Observables.slnx -c Release` — 0 error / 0 warning
- [ ] 全部生成器测试通过
- [ ] TrimTests publish 无 IL 警告
- [ ] Doc localization parity 14 项目通过

## Risks/Considerations

- **命名冲突**：`Nullability` 移到全局后，域内若有同名类型会冲突 — 已检查无冲突
- **命名空间可见性**：`<Using Include="Observables.SourceGenerators.Shared" />` 已在 props 中全局导入，域代码无需修改 using
- **扩展方法歧义**：全局和域内不能同时定义相同签名的 `ReportDiagnostics` — 步骤 6 确保从域内删除
- **RestAPI 特殊性**：RestAPI 有额外的单条 Diagnostic 重载，保留在域内不提取
