# com.abilitykit.network.transport.litenet

> AbilityKit 的**可选可靠 UDP 传输**（`ITransport` 实现），基于 [LiteNetLib](https://github.com/RevenantX/LiteNetLib)（`DeliveryMethod.ReliableOrdered`）。适合快节奏、丢包、低延迟场景（比 TCP 延迟更低）。纯加法包。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Transport.LiteNet`
- **依赖**：`com.abilitykit.network.runtime` 0.1.0 + **LiteNetLib**（.NET 经 NuGet `LiteNetLib 2.1.4`；Unity 需 `LiteNetLib.dll`，经 NuGet-for-Unity 或手动放入）
- **类型**：1 个 —— `LiteNetTransport : ITransport`（基于 `EventBasedNetListener` + `UnsyncedEvents`）

## 适用
- 快节奏 / 丢包 / 低延迟（FPS、动作、竞技）。
- 用 LiteNetLib 的 `ReliableOrdered` 通道承载 `ConnectionManager` 的成帧字节（可靠传输两层叠加，冗余但兼容 —— 与 WebSocket 同理）。

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

- ctor：`LiteNetTransport(connectionKey: "abilitykit")` —— 客户端与服务端的 connection key 必须一致。
- 用 `UnsyncedEvents = true`：事件在 LiteNetLib 内部线程触发，**无需外部 PollEvents/tick**，符合 `ITransport` 无 tick 接口。

## 注意（与 WebSocket 同）
- 这是**客户端**传输。端到端经可靠 UDP，**服务端网关也需要 UDP/LiteNetLib 监听端**（当前 `TcpTransportServer` 只讲 TCP）。服务端 UDP 支持是独立后续工作。
- LiteNetLib 自带连接管理 + 可靠传输，作为 `ITransport` 用时与上层 `ConnectionManager` 的成帧/重连是叠加关系（功能不冲突，略冗余）。

## 相关
- 默认传输 → `com.abilitykit.network.runtime`（`TcpTransport`）
- 另一可选传输 → `com.abilitykit.network.transport.websocket`（WebSocket）
- 组装根 → `com.abilitykit.network.sdk`
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
