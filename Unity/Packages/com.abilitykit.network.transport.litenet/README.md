# com.abilitykit.network.transport.litenet

> AbilityKit 的**可选可靠 UDP 传输**（`ITransport` 实现），基于 [LiteNetLib](https://github.com/RevenantX/LiteNetLib)，使用 `DeliveryMethod.ReliableOrdered`。它是面向低时延游戏网络的候选实现；当前仓库没有真实弱网或 TCP 对比数据，不能据此声称已获得更低延迟。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Transport.LiteNet`
- **依赖**：`com.abilitykit.network.runtime` 0.1.0 + **LiteNetLib**（.NET 经 NuGet `LiteNetLib 2.1.4`；Unity 需 `LiteNetLib.dll`，经 NuGet-for-Unity 或手动放入）
- **类型**：1 个 —— `LiteNetTransport : ITransport`（基于 `EventBasedNetListener` + `UnsyncedEvents`）

## 适用与边界

- 候选场景是 FPS、动作和竞技游戏，但是否优于 TCP 必须在目标部署网络、消息模型和弱网参数下压测。
- 当前固定使用 LiteNetLib `ReliableOrdered` 通道承载 `ConnectionManager` 的成帧字节。LiteNetLib 的连接/可靠机制与上层心跳、重连会叠加，应联合验证超时和恢复时序。
- 这不是“把 TCP 端口改成 UDP 端口”即可运行的替换：服务端必须实现相同 connection key、LiteNetLib 会话和 AbilityKit frame protocol。

## Unity 设置

**无需手动操作** —— `LiteNetLib.dll`（netstandard2.1）已内置在 `Runtime/Plugins/`，Unity 自动引用。

## 用法

```csharp
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new LiteNetTransport(connectionKey: "your-shared-key"))
    .ConfigureConnection(o => { /* heartbeat / reconnect / FrameCodec */ })
    .Build();
sdk.Open(host, port);
```

- ctor：`LiteNetTransport(connectionKey: "abilitykit")`；客户端与服务端的 connection key 必须一致。
- `UnsyncedEvents = true` 使事件由 LiteNetLib 内部线程触发，无需外部 `PollEvents`。默认 inline dispatcher 会让上层回调继续运行在该线程；Unity 业务应显式派发到主线程。

## 服务端与验证状态

- 这是客户端 transport。当前仓库没有 AbilityKit LiteNet/UDP 网关，也未发现业务运行时消费者，因此尚未形成端到端默认链路。
- `AbilityKit.Network.Transport.LiteNet.Tests` 只有本机 UDP echo round-trip，证明基础连接和收发，不覆盖真实弱网、NAT、移动网络切换、吞吐、延迟对比、重连耗尽或长时间稳定性。
- 在服务端监听、部署网络、线程派发和恢复策略共同验收前，应将本包视为 E0 实现 + E3 局部回环测试，而不是生产成熟 transport。

## 相关
- 默认传输 → `com.abilitykit.network.runtime`（`TcpTransport`）
- 另一可选传输 → `com.abilitykit.network.transport.websocket`（WebSocket）
- 组装根 → `com.abilitykit.network.sdk`
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
