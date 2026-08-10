# com.abilitykit.network.runtime

> AbilityKit 网络运行时原语层。传输抽象、连接管理、成帧/会话、请求-响应、时钟同步、插值、网络条件模拟。
> 所有上层网络包（sdk / room / battle / transport.*）都构建在此之上。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Runtime`（核心）+ `AbilityKit.Network.Abstractions`（接口）
- **依赖**：`AbilityKit.Core`（日志）

## 目录结构

```
Runtime/Network/
├── Abstractions/          ITransport / IConnection / IReconnectableConnection / IDispatcher / IService
├── Protocol/              NetworkFrameCodec / NetworkPacketHeader / IFrameCodec / LengthPrefixedFrameCodec
├── Runtime/
│   ├── Transports/         TcpTransport（内置；WebSocket/LiteNetLib/InMemory 在可选传输包里）
│   ├── Connections/        ConnectionManager（封装 ITransport → IConnection，含心跳/重连/分帧）
│   ├── RequestResponse/    RequestClient（opCode + seq 的请求-响应配对）
│   ├── Gateway/            IGatewayConnection + GatewayConnection（seq 匹配 + 推送分发）
│   ├── TcpGateway/         TcpGatewayResponseCodec（网关响应解码）
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
| `IGatewayConnection` | 网关级连接（SendRequestAsync + 推送注册 + seq 匹配） |
| `IDispatcher` | 回调线程派发（InlineDispatcher / SynchronizationContextDispatcher） |
| `IFrameCodec` | 成帧编解码（LengthPrefixedFrameCodec 默认） |
| `INetworkDiagnostics` | 统一网络诊断快照 |

## 关键类型

- **`ConnectionManager`**：封装 `ITransport` → `NetworkSession`（成帧）→ `IConnection`。内置心跳、重连（`ReconnectBackoffPolicy`）、推送分发。
- **`RequestClient`**：在 `IConnection` 之上做 seq 配对的请求-响应（带超时 + 取消）。
- **`SyncClock` / `ServerClockEstimator`**：客户端时钟与服务端时钟的偏移估计。
- **`FastReconnectSession`**：断线快重连状态机（Connected → Disconnected → Resuming → AwaitingFullSnapshot → Recovered）。
- **`SyncHealthEvent` / `SyncHealthEventBuffer`**：同步健康事件（Info/Warning/Error）。
- **`NetworkDiagnosticsSnapshot`**：统一诊断快照（RTT/帧差/resync/快照/输入计数 + `IsHealthy`）。
- **`NetworkConditioningMiddleware`**：模拟延迟/抖动/丢包（用于测试）。

## 相关
- 组装根 → `com.abilitykit.network.sdk`
- 房间会话 → `com.abilitykit.network.room`
- 战斗数据面 → `com.abilitykit.network.battle`
- 可选传输 → `network.transport.websocket` / `.litenet` / `.inmemory`
- 序列化模型 → `com.abilitykit.protocol` README
