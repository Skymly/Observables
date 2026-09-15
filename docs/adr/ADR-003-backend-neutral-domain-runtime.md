# ADR-003: 域运行时保持后端中立，R3 桥接移入 `Observables.<Feature>.R3`

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-09-15 |
| **关联 Issue** | [#363](https://github.com/Skymly/Observables/issues/363) |

## 背景

`AGENTS.md`「反应式后端规则」写着：R3 包不引用 System.Reactive，Reactive 包不引用 R3。0.2.2 已发的 20 个包里，这句话对 Reactive 侧不成立。

`Observables.<Feature>.Reactive` 这个 nupkg 的 `lib/` 下有两个程序集：域运行时 `Observables.<Feature>.dll` 和桥接 `Observables.<Feature>.Reactive.dll`。桥接那个确实只引用 System.Reactive，但域运行时那个引用 R3——因为 R3 桥接类型（`MqttObservable`、`RedisObservable`、……）就住在域运行时里。nuspec 只声明 System.Reactive，于是包里躺着一个依赖没被声明的程序集。

`Observables.Events` 和 `Observables.RestAPI` 不在此列：它们的域运行时本来就没有 R3 桥接类型。

八个受影响的域是 Grpc、Mqtt、Nats、Postgres、Redis、SignalR、Sse、WebSocket。

### PackVerify 为什么没拦住

`build/PackCsprojReader.cs` 收集 nuspec 应有的依赖时，对 lib 项目的 `PackageReference` 有一条：

```csharp
if (reference.IsPrivateAssetsAll || BackendPackageIds.Contains(reference.Id))
{
    continue;
}
```

`BackendPackageIds` 就是 `{ R3, System.Reactive }`。这行本意是别让 R3 包因为某个 lib 引用了 System.Reactive 就被要求声明它——但它同时把「Reactive 包的 lib 引用了 R3」这件事一起吞了。`NupkgVerifier.ForbiddenNuspecSubstrings` 只看 nuspec 文本和包内文件路径，看不到程序集内部的引用表。

## 决策

**域运行时保持后端中立；R3 桥接类型移到新建的 `Observables.<Feature>.R3` 库项目，与已有的 `Observables.<Feature>.Reactive` 对称。**

即 #363 的选项 1。选项 2（承认现状，给 Reactive nuspec 补上 R3 依赖）被否决：它和上面那句后端规则直接冲突，等于让每个 System.Reactive 用户永久拖一个 R3，两个包的存在意义就没了。

拆分成本比票里估的低。八个域的 R3 面积各自只有一个文件——`<Feature>Observable.cs`，Grpc 多一个 `GrpcProtocol.cs`；公共 API 各 1–7 行，全部集中在 `<Feature>Observable` 这一个静态类上。`Observables.Events` 已经是目标形态，不是凭空设计。

目标布局（`Observables.Mqtt` 为例）：

| 程序集 | 依赖 | 进哪个包 |
|--------|------|----------|
| `Observables.Mqtt.dll` | MQTTnet | 两个包都进 |
| `Observables.Mqtt.R3.dll` | 上面那个 + R3 | 仅 `Observables.Mqtt.R3` |
| `Observables.Mqtt.Reactive.dll` | 上面第一个 + System.Reactive | 仅 `Observables.Mqtt.Reactive` |

命名空间不变（仍是 `Observables.Mqtt`），所以消费者源码不动；生成的代理已经通过 `Observables.<Feature>.R3` 包拿到新程序集。

### 落地顺序

1. 本 ADR + `AGENTS.md` 项目组成表 + PackVerify 同时接受新旧两种布局（Solution Items）。
2. 每域一个 Issue、一个 PR，`Blocked by` 第 1 步。
3. 全部域完成后收紧 PackVerify，只接受新布局（第二个 Solution Items PR）。
4. Docs 仓架构页 / 安装页同步。

第 1 步的 PackVerify 改动是一个棘轮：新增检查「`.Reactive` 包里的任何 lib 程序集都不得引用 R3」（`.R3` 包对 System.Reactive 同理），并带一张仍未拆分的域名单。域拆完就把它从名单里划掉，名单空了就删掉名单本身。这样「这个域拆完了没有」由 CI 回答，不靠人记。

## 后果

- **正面**：`AGENTS.md` 那句后端规则从口头约定变成 CI 能验的事实；System.Reactive 用户不再需要 R3；nuspec 与包内容一致。
- **正面**：新增的检查读的是程序集引用表，而不是 csproj 或 nuspec 文本，所以它验的是真正发出去的东西。
- **负面**：每个域多一个项目、多一份 PublicAPI 基线。8 域 × 4 TFM 的基线要挪行。
- **负面**：二进制层面是破坏性变更——直接对 `Observables.Mqtt.dll` 编译并用了 `MqttObservable` 的人，升级后需要重新编译。类型转发救不了：`TypeForwardedTo` 要求源程序集引用目标程序集，那正是要去掉的引用。0.2.x 未到 1.0，按版本策略可接受，发版说明里点名。
- **负面**：迁移期间两种布局并存，PackVerify 的名单是临时债，第 3 步清掉。

## 参考

- [#363](https://github.com/Skymly/Observables/issues/363)
- `AGENTS.md`「反应式后端规则」「每个 Feature 的项目组成」
- `build/PackCsprojReader.cs`、`build/NupkgVerifier.cs`
- 先例：`Observables.Events`（域运行时零 R3）、`Observables.RestAPI`（[#512](https://github.com/Skymly/Observables/issues/512) 清掉死引用后同样中立）
