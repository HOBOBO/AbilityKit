# com.abilitykit.network.runtime

> AbilityKit 网络运行时原语层。传输抽象、连接管理、成帧/会话、请求-响应、时钟同步、插值、网络条件模拟。
> 所有上层网络包（sdk / room / battle / transport.*）都构建在此之上。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Runtime`（核心）+ `AbilityKit.Network.Abstractions`（接口）
- **依赖**：`AbilityKit.Core` 与 `AbilityKit.Protocol`

## 安装与首次使用

安装条件、版本和直接依赖以 [package.json](package.json) 为准。从[文档索引](Documentation~/README.md)进入；[首次使用](Documentation~/Usage.md)包含本地生成数据的完整成帧例子与连接选择说明。下方条件模拟代码是装配片段，`clockMs`、`inputOpCode` 与 pipeline 均须由宿主提供。

## 目录结构

```
Runtime/Network/
├── Abstractions/          ITransport / IConnection / IReconnectableConnection / IDispatcher / IService
├── Protocol/              NetworkFrameCodec / NetworkPacketHeader / IFrameCodec / LengthPrefixedFrameCodec
├── Runtime/
│   ├── Transports/         TcpTransport（内置；WebSocket/LiteNetLib/InMemory 在可选传输包里）
│   ├── Connections/        ConnectionManager（封装 ITransport → IConnection，含心跳/重连/分帧）
│   ├── RequestResponse/    RequestClient + NetworkProtocolSession（协议请求/响应 + 推送分发）
│   ├── TcpGateway/         TcpGatewayResponseCodec（既有网关协议适配）
│   ├── Sync/               SyncClock / ServerClockEstimator / FastReconnectSession / ReconnectBackoffPolicy / SyncHealthEvent / NetworkDiagnosticsSnapshot / ClientSyncRecoveryCoordinator / SnapshotSendQueue 等
│   ├── Interpolation/      InterpolationDiagnostics / RemoteInterpolationPlayback（远程插值播放）
│   ├── Conditioning/       NetworkConditioningMiddleware / NetworkConditionProfile（延迟/抖动/丢包模拟）
│   ├── LagCompensation/    ServerRewindLagCompensationService（服务端回滚命中检测）
│   └── DemoHarness/        DemoHarnessRunner（⚠️ 演示/测试基础设施，计划迁出）
```

## 核心接口

| 接口 | 职责 |
|------|------|
| `ITransport` | 原始字节传输（Connect/Send/BytesReceived）。TcpTransport 内置；WebSocket/LiteNetLib/InMemory 可选 |
| `IConnection` | 帧级连接（Open/Send/PacketReceived/ServerPushReceived）。ConnectionManager 是默认实现 |
| `IReconnectableConnection` | 带重连控制的连接 |
| `INetworkProtocolSession` | 通用协议会话（SendRequestAsync + 推送注册 + seq 匹配） |
| `IDispatcher` | 回调线程派发（InlineDispatcher / SynchronizationContextDispatcher） |
| `IFrameCodec` | 成帧编解码（LengthPrefixedFrameCodec 默认） |
| `INetworkDiagnostics` | 统一网络诊断快照 |

## 关键类型

- **`ConnectionManager`**：封装 `ITransport` → `NetworkSession`（成帧）→ `IConnection`。内置心跳、重连（`ReconnectBackoffPolicy`）、推送分发。
- **`RequestClient`**：在 `IConnection` 之上做 seq 配对的请求-响应（带超时 + 取消）。
- **`NetworkProtocolSession`**：在 `IConnection` 之上提供协议请求与推送的便捷会话；默认使用 Raw response body。Gateway envelope 等协议由调用方显式传入 `IRequestResponseEnvelopeCodec`，因此 runtime 不绑定具体网关。
- **`SyncClock` / `ServerClockEstimator`**：客户端时钟与服务端时钟的偏移估计。
- **`FastReconnectSession`**：断线快重连状态机（Connected → Disconnected → Resuming → AwaitingFullSnapshot → Recovered）。
- **`SyncHealthEvent` / `SyncHealthEventBuffer`**：同步健康事件（Info/Warning/Error）。
- **`NetworkDiagnosticsSnapshot`**：统一诊断快照（RTT/帧差/resync/快照/输入计数 + `IsHealthy`）。
- **`NetworkConditioningMiddleware`**：模拟延迟/抖动/丢包（用于测试）。

### 请求会话生命周期

- `RequestClient` 和 `NetworkProtocolSession` 借用底层 `IConnection`。释放只终止自身请求和订阅；连接由创建者关闭或释放。如果连接的主动关闭不发 `Disconnected`（例如 `ConnectionManager.Close()`），关闭方应先释放自己持有的请求会话。
- 收到 `Disconnected` 或 `Error` 时，只终结当时还在等待的请求，不自动重发。未释放的会话可随底层连接恢复后发起新请求；旧请求的迟到响应不会完成新请求。
- 响应、失败、取消、超时和释放中，先取走请求的一方决定结果。释放不等待已经在执行的发送、解码或推送回调；回调内释放时，尚未获准进入的后续推送处理器跳过。释放后会话不再报告已连接，不再接收新订阅。
- 推送事件的 `ArraySegment<byte>` 只在回调期间有效。按操作码注册的处理器接收传输载荷副本；非路由回退路径的同批处理器共享副本，请按只读载荷使用。

### 主动关闭与调度队列

- `NetworkSession.Start()` 启动新接收周期。`Stop()` 先撤销周期再解绑传输；已排队的旧 I/O 与通知执行时丢弃，半帧不带入下一次启动。已进入中间件的工作不强行中断，但结束时不会向新周期分发或发送旧包。
- `ConnectionManager.Close()` / `Dispose()` 使主动关闭前尚未执行的连接通知和重连通知失效。主动关闭仍不合成 `Disconnected`，不改变远端断线的重连策略。宿主应串行调用生命周期方法；关闭不是等待在途回调结束的屏障。

## 可复现场景

`NetworkConditionScenario` 的阶段相对于中间件收到的第一个包计时；后定义的匹配阶段覆盖前面的阶段。方向和 opcode 可选，用于区分上行输入、下行快照。默认档案在所有阶段之外生效：

```csharp
var loss = new NetworkConditionProfile(0, 0, 1d, 0d, 0);
var scenario = new NetworkConditionScenario(NetworkConditionProfile.Lan,
    new NetworkConditionScenario.Phase(2000, 5000, loss,
        NetworkConditionDirection.Outbound, inputOpCode));
var middleware = new NetworkConditioningMiddleware(scenario, clockMs, seed: 42,
    decisionCapacity: 1024, maxPendingPackets: 4096);
// 挂载到 NetworkPipeline 后，由宿主持续调用 middleware.Advance(clockMs())。
```

`SnapshotDecisions()` 返回有界的包级观察时刻、计划投递时刻、乱序和丢弃原因；连同场景定义和 `Seed` 保存即可检查重现过程。切换场景前应先导出旧实例的记录，`ClearPending()` 会丢弃排队的旧包。模拟工作在协议包层，不等价于 TCP 重传、socket 断线或系统级拥塞；这些情况仍需实际传输链路测试。实时宿主必须持续以真实墙钟推进连接的 `Tick`。

严格 DSL 回放应改用 `VirtualNetworkConditionLink`，不创建 socket。`VirtualNetworkScenarioPlan` 先把 `network.phase` 命令编译成从虚拟 `t=0` 起算的条件窗口；`VirtualNetworkScenarioPlayer` 再稳定排序并执行 `network.packet` / `network.disconnect` / `network.reconnect`。`AdvanceTo(atMs)` 会经过每个中间投递截止点，包的回调因此看到准确的虚拟到达时间。在每个时间点先冲刷旧包、再执行当前命令；同一时间点的命令保持源顺序。`Disconnect()` 清空未投递包并阻断新包，`Reconnect()` 允许 DSL 显式补投遗漏的权威帧。虚拟模式使用固定算法随机流，必须保持同一脚本、seed、协议包顺序和运行逻辑才能逐项重放。`Events`、`SnapshotDecisions()` 和战斗载体收到的帧共同构成检查轨迹；使用完毕释放 link。

Moba 的 `NetworkConditionController` 仍连接实时 `ConnectionManager`，用于交互调试，不是虚拟时钟的严格复现入口。`TestScenario.Commands` 或 BattleFlow DSL 可携带 `network.phase` / `network.disconnect` / `network.reconnect` / `network.packet`。Moba 的 `MobaVirtualFrameCarrier` 把 `FramePacket` 作为虚拟包的类型化载荷，调用方把投递回调绑定到 `BattleLogicSession.InjectRemoteFrame`，即可进入原有预测、补帧和回滚管线，而不会调用实际连接的 Close/Connect。需要精确模拟带宽时，应同时传入线上同款帧序列化回调，使虚拟队列使用真实 payload 字节数计算发送耗时。

## 相关
- 组装根 → `com.abilitykit.network.sdk`
- 房间会话 → `com.abilitykit.network.room`
- 战斗数据面 → `com.abilitykit.network.battle`
- 可选传输 → `network.transport.websocket` / `.litenet` / `.inmemory`
- 序列化模型 → `com.abilitykit.protocol` README
