# Spec: <DomainName>

> **版本**：vX.Y.Z（与 `eng/Observables.Package.props` 对齐）
> **关联 Design Doc**：[docs/design/<DomainName>.md](../design/<DomainName>.md)
> **关联 ADR**：ADR-XXX（如有）

## API 面

### 运行时接口与 Attribute

（公共 API、特性类、消费者可见类型）

### 生成器产出

（生成的类名、命名空间、方法签名；R3 与 Reactive 分节）

## 诊断 ID

| ID | 级别 | 触发条件 | helpLinkUri 锚点 |
|----|------|----------|------------------|
| OBSxxxx | Warning / Error | ... | `obsxxxx` |

共享分析器诊断（`OBS0001`、`OBS*007`）见 [Observables.Docs diagnostics](https://skymly.github.io/Observables.Docs/diagnostics.html)。

## 不变量

1. ...

## 兼容基线

- 运行时 TFM：`netstandard2.0` / `net8.0` / `net9.0`（以域 csproj 为准）
- 生成器：`netstandard2.0`

## 不在范围内

- ...
