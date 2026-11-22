# ADR-004 — MQTT 传输接口与 MQTTnet 适配包

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-11-22 |

## 背景

MQTTnet 5 调整了客户端类型所在的命名空间，客户端最低支持 .NET 8。Observables 的 MQTT 代理目前在运行时、公共 API 和生成代码里直接依赖 MQTTnet 4。选择任一 MQTTnet 主版本作为共同依赖，都会迫使另一主版本的用户迁移。

## 决策

将发布、订阅、退订和接收消息收拢到 `Observables.Mqtt.IMqttTransport`。接收处理器在其异步回调期间使用只读载荷。传输实现负责把 broker 拒绝转换成异常。功能标志向代理报告可用能力，为 MQTT 5 请求–响应保留扩展点。连接、重连、断开和处置仍由调用方管理。MQTT 约定保留 QoS 0 订阅、QoS 1 发布和同一传输实例上相同过滤器的订阅引用计数。

| NuGet 包 | 内容与依赖 |
|----------|------------|
| `Observables.Mqtt` | 中立运行时，仍使用 `Observables.Mqtt.dll`。 |
| `Observables.Mqtt.R3` / `Observables.Mqtt.Reactive` | 对应桥接和分析器，共用 `Observables.Mqtt`。由消费者选择适配包。 |
| `Observables.Mqtt.MQTTnet4` / `Observables.Mqtt.MQTTnet5` | 依赖中立运行时和对应 MQTTnet 主版本的适配程序集。 |

这三个中立包是 MQTT 域的扩展布局，消费者的反应式主包仍按后端选择。运行时 DLL 只由一个包交付，适配包可以与任一反应式后端独立组合。MQTTnet 两个主版本使用相同的 NuGet 包 ID，一个应用选用一个 MQTTnet 适配包。MQTTnet 4 适配包保留 `netstandard2.0`，MQTTnet 5 适配包从 .NET 8 起提供。

`MqttService.For<T>`、`RegisterClient` 和直接调用反应式桥接的入口改为接收 `IMqttTransport`。删除以 MQTTnet 4 `IMqttClient` 为参数的签名。升级时先用所选适配包的 `MqttNetTransport.Wrap(client)` 包装已连接的客户端，并重新编译生成的代理。`[Mqtt(name)]` 的名字解析方式保持一致。同一个客户端实例在同一适配包中得到同一个包装实例，使订阅计数覆盖不同代理。

MQTTnet 4 适配包自 `1.0.0` 进入维护模式，前提是两个适配包都已通过各自 broker 的发布、订阅、释放测试，以及消费包的组合验证。维护模式接受安全和正确性修复，后续 MQTT 5 专有功能在 MQTTnet 5 适配包提供。

## 后果

- MQTT 代理、反应式桥接和生成代码可在不选择 MQTTnet 主版本的情况下构建。各适配包保有独立的上游版本边界。
- 既有 MQTT 用户升级需要添加一个适配包并修改客户端绑定，接口声明无需随 MQTTnet 版本变化。
- PackVerify 校验中立包的 NuSpec 依赖和程序集引用。NuGet smoke 验证两个适配包可分别与两种反应式主包组合。

## 参考

- [路线图 R2-A 与 R4](../ROADMAP.md)
- [ADR-003 — 后端中立域运行时](ADR-003-backend-neutral-domain-runtime.md)
- [MQTTnet 主版本升级指南](https://github.com/dotnet/MQTTnet/wiki/Upgrading-guide)
