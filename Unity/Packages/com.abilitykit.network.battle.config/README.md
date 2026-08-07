# com.abilitykit.network.battle.config

> High-level fluent builder (`NetworkBattleConfig`) for `NetworkTransportOptions`. 封装标准 room-gateway 协议预设（opcodes + auth handshake + reliable-event ack + full-state-sync + command-sequence wrapping），游戏只需提供自己的 serialize/deserialize 回调。将 ~30 行 options 初始化缩减到 ~7 行。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Battle.Config`
- **依赖**：`network.battle` + `network.runtime` + `protocol.room`

## 用法

```csharp
var options = new NetworkBattleConfig()
    .WithGateway(host, port)
    .WithTcpTransport()                     // 或 WithTransportFactory(...) / WithInjectedConnection(...)
    .WithSession(token, battleId, roomId)   // session 身份
    .UseRoomGatewayProtocol(battleId, roomId) // opcodes + auth + ack + resync（标准协议全包）
    .WithInputSerializer(                    // 游戏专属：输入序列化
        serializeSubmitInput,
        deserializeSubmitInputResponse)
    .WithSnapshotDeserializer(               // 游戏专属：快照反序列化
        deserializeSnapshotPushed)
    .WithReliableEventCursor(                // 可选：可靠事件游标（重连续接）
        () => epoch, () => lastAck)
    .Build();                                // → NetworkTransportOptions（已验证必填字段）

var transport = new NetworkTransport(options);
```

## `UseRoomGatewayProtocol` 自动设置的（游戏不用管）

- 8 个标准 room-gateway opcodes（SubmitBattleInput=107 / SnapshotPushed=9002 / DeltaSnapshotPushed=9003 / ReliableBattleEventsPushed=9005 / AckReliableBattleEvents=116 / RequestFullStateSync=108 / RenewSession=120 / SubscribeStateSync=103）
- Auth 握手回调（`SerializeRenewSession` → WireRenewSessionReq + `SerializePostAuthenticationWithReliableEventCursor` → WireSubscribeStateSyncReq）
- Reliable-event ack 序列化/反序列化（WireAckReliableBattleEventsReq/Res）
- Reliable-event push 反序列化（WireReliableBattleEventPush）
- Full-state-sync 请求序列化/反序列化（WireRequestFullStateSyncReq/Res）
- Command-sequence wrapping（`PrepareSubmitInput` + `RewriteSubmitInputFrame` — 标准 Interlocked.Increment + 帧重写）

## 游戏只需提供的

| 回调 | 用途 |
|------|------|
| `WithInputSerializer(serialize, deserializeResponse)` | 输入上行：把游戏 input struct 序列化为 WireSubmitBattleInputReq 字节 + 反序列化响应 |
| `WithSnapshotDeserializer(deserialize)` | 快照下行：把 WireStateSyncSnapshotPush 字节解码为游戏快照对象 |

## 相关
- 引擎 → `com.abilitykit.network.battle`
- 组装根 → `com.abilitykit.network.sdk`
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
