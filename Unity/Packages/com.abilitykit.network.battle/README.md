# com.abilitykit.network.battle

> AbilityKit 多人联网 SDK 的**战斗数据面引擎**。位于 `network.sdk`（组装根）之上、各玩法的客户端同步/预测策略之下的"中间层"：把"输入上行 + 快照/帧/事件下行 + 可靠事件 + 重连 resync"这套通用战斗数据流，抽象成一个 **opcode + callback** 的玩法无关引擎。每个玩法只提供自己的 opcodes / wire DTO / codec。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Battle`（Projection 子命名空间 `AbilityKit.Network.Battle.Projection`）
- **依赖**：`com.abilitykit.network.sdk`、`com.abilitykit.network.runtime`、`com.abilitykit.game.battle.runtime`、`com.abilitykit.world.networkfragments`、`com.abilitykit.world.snapshot`
- **状态**：MOBA demo 的 FrameSync 生产热路径在用；服务端（`MobaBattleProtocolMapper`/`MobaBattleRuntimeAdapter`）与 shooter（`ShooterActorProjectionProducer`）使用其中的 Projection 抽象。

> 本包前身是 `com.abilitykit.game.battle.transport.runtime` 里的通用引擎层。2026-08-06 的 P2.1 把通用引擎搬到这个中立包并改名命名空间；原 `game.battle.transport.runtime` 包（含遗留 `Moba/` 子树）已整体删除（Console demo 迁移到统一 SDK）。

## 在网络栈中的位置

```
network.sdk        NetworkSdkBuilder → NetworkSdkClient（连接/请求-响应/推送/重连）
  ↑ 自建 SDK client（由 Options.TransportFactory 注入 ITransport）
network.battle(本包)  NetworkTransport：opcode-keyed 输入提交 + 下行分发 + 重试/resync
  ↑ 经 IBattleLogicTransport 喂给玩法 session（BattleLogicSession）
玩法同步策略        各 demo 自写：moba 的 ClientPredictionDriverModule（双世界）/ shooter 的 SyncController
```

## 两种组合方式

| 类型 | 接口 | 适用 |
|------|------|------|
| `NetworkTransport` | `IBattleLogicTransport` | **完整战斗数据面引擎**：自建 `NetworkSdkClient`，输入提交（带服务端权威帧重试）、CreateWorld/Join/Leave、帧/快照/可靠事件下行分发、resync。 |
| `GenericNetworkClient` | `INetworkClient` | **精简请求/推送适配器**：自建 `NetworkSdkClient`，只暴露 `Connect/Disconnect/SendRequestAsync/SendServerPushAsync` + `OnServerPush`。 |

另有 `NullBattleLogicTransport`（`IBattleLogicTransport` 空实现，离线/测试）与 `Projection/`（`IActorProjectionProducer`/`IActorProjectionConsumer`/`ActorProjectionData`/`ActorProjectionFields`，逻辑→视图/快照投影抽象，被两个 demo + 服务端共用）。

## NetworkTransportOptions（核心：纯 callback 配置表）

`NetworkTransport` 的全部玩法差异都收敛在这张表里 —— **没有任何玩法类型**，只有 opcode 常量与 `Func` 回调。新玩法接入 = 填这张表。

- **连接 / 传输**：`Host`/`Port`、`TransportFactory : Func<ITransport>`（必填）、`FrameCodec : IFrameCodec`
- **会话 / 鉴权**：`SessionToken`、`OpRenewSession`+`SerializeRenewSession`、`OpPostAuthentication`+`SerializePostAuthentication`/`SerializePostAuthenticationWithReliableEventCursor`、`GetReliableEventEpoch`/`GetReliableEventLastAcknowledgedSequence`
- **世界生命周期**（fire-and-forget）：`OpCreateWorld`+`SerializeCreateWorld`、`OpJoin`+`SerializeJoin`、`OpLeave`+`SerializeLeave`
- **输入上行**：`OpSubmitInput`、`SubmitInputRetryFrameLead`(=2)、`PrepareSubmitInput`、`SerializeSubmitInput`、`DeserializeSubmitInputResponse`（配了才走请求-响应+重试）、`RewriteSubmitInputFrame`、`OnSubmitInputAck`
- **下行 opcodes + 反序列化器**：`OpFramePushed`+`DeserializeFramePushed`(→FramePacket)、`OpSnapshotPushed`/`OpDeltaSnapshotPushed`+`DeserializeSnapshotPushed`、`OpReliableEventsPushed`+`DeserializeReliableEventsPushed`
- **可靠事件 ack**：`OpAcknowledgeReliableEvents`+`SerializeAcknowledgeReliableEvents`/`DeserializeAcknowledgeReliableEventsResponse`
- **全量 resync**：`OpRequestFullStateSync`+`SerializeRequestFullStateSync`/`DeserializeRequestFullStateSyncResponse`

`NetworkSubmitInputResponse`（上行回执）：`Accepted`/`ServerFrame`/`ReasonCode`/`RetryAtAuthoritativeFrame`/`Status`/`Message`。

## NetworkTransport 公共面

```csharp
public sealed class NetworkTransport : IBattleLogicTransport, IDisposable
{
    public NetworkTransport(NetworkTransportOptions options, IDispatcher dispatcher = null);
    public NetworkTransportOptions Options { get; }

    public event Action<FramePacket> FramePushed;            // OpFramePushed
    public event Action<object>     StateSyncSnapshotPushed; // OpSnapshotPushed / OpDeltaSnapshotPushed
    public event Action<object>     ReliableEventsPushed;    // OpReliableEventsPushed
    public event Action ConnectionEstablished;              // TCP 建连/重连（早于鉴权）
    public event Action ConnectionClosed;

    public void Connect();
    public void Disconnect();
    public void SendCreateWorld(CreateWorldRequest request); // fire-and-forget
    public void SendJoin(JoinWorldRequest request);
    public void SendLeave(LeaveWorldRequest request);
    public void SendInput(SubmitInputRequest request);       // 配了 DeserializeSubmitInputResponse → 请求-响应 + 权威帧重试一次；否则 fire-and-forget
}
```

**服务端权威帧重试**：`SendInput` 走请求-响应时，若回执 `RetryAtAuthoritativeFrame == true`，引擎用 `RewriteSubmitInputFrame` 把输入帧改为 `ServerFrame + SubmitInputRetryFrameLead` 后**重试一次**。

## 信封类型（跨包中立框架代码，本引擎消费）

| 类型 | 包 | 说明 |
|------|----|------|
| `FramePacket`（命名空间 `AbilityKit.Ability.Host`） | `com.abilitykit.world.networkfragments` | 帧包信封 `(WorldId, FrameIndex, IReadOnlyList<PlayerInputCommand>, WorldStateSnapshot?)`，实现 `ISnapshotEnvelope` |
| `PlayerInputCommand` | `com.abilitykit.world.framesync` | `(FrameIndex, PlayerId, int OpCode, byte[] Payload)` |
| `WorldStateSnapshot` | `com.abilitykit.host` | `(int OpCode, byte[] Payload)` 不透明快照载荷 |
| `FramePacketNetAdapter` | `com.abilitykit.host.extension` | 把 `FramePacket` 路由进双 jitter-buffer + 喂 `FrameSnapshotDispatcher` |
| `FrameSnapshotDispatcher` | `com.abilitykit.world.snapshot` | opcode-keyed 类型化快照分发 |

## 如何为新玩法配置

完整范本：moba `com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Gateway/NetworkTransportOptionsFactory.cs`（同时演示 FrameSync 与 Room-Gateway 两种 opcode/wire 配置）。最小骨架（DTO/codec 由玩法自带）：

```csharp
var options = new NetworkTransportOptions
{
    Host = host, Port = port,
    TransportFactory = () => new TcpTransport(),     // 注入传输（可换 WebSocket/KCP/GameFramework 桥接）
    FrameCodec = new LengthPrefixedFrameCodec(),
    SessionToken = sessionToken,
    OpSubmitInput = YourOpCodes.SubmitBattleInput,
    PrepareSubmitInput = req => AddCommandSequence(req),
    SerializeSubmitInput = req => YourBinary.Serialize(req),
    DeserializeSubmitInputResponse = bytes => ToResp(YourBinary.Deserialize<YourSubmitRes>(bytes)),
    RewriteSubmitInputFrame = (req, serverFrame) => RewriteFrame(req, serverFrame),
    OpSnapshotPushed = YourOpCodes.SnapshotPushed,
    OpDeltaSnapshotPushed = YourOpCodes.DeltaSnapshotPushed,
    DeserializeSnapshotPushed = bytes => YourSnapshotCodec.Decode(bytes),
    OpRequestFullStateSync = YourOpCodes.RequestFullStateSync,
    SerializeRequestFullStateSync = (reason, frame) => YourBinary.Serialize(new YourFullStateReq { ... }),
};
var transport = new NetworkTransport(options, dispatcher);
transport.StateSyncSnapshotPushed += snap => { /* 喂给你的同步策略 */ };
transport.Connect();
```

## 两种契约形态（framesync vs statesync）— 引擎契约中立

`NetworkTransport` 支持两种接入契约，按玩法选其一（不要混用）：

| 形态 | 适用 | 输入上行 | 下行分发 |
|------|------|----------|----------|
| **typed / fire-and-forget**（framesync，moba 用） | 客户端预测 + 权威对账，输入无需 per-submit 结果 | `void SendInput(req)` | 订阅 `FramePushed` / `StateSyncSnapshotPushed` / `ReliableEventsPushed`（引擎解码后的对象） |
| **raw / awaitable**（statesync，服务端权威） | 需要 per-submit 服务端结果 + 复用既有 raw apply 管线 | `await SendInputAsync(req)` → `NetworkSubmitInputResponse`（`Accepted`/`ServerFrame`/`ShouldResync`/`ServerTicks`…） | 订阅 `RawServerPushReceived(opCode, payload)`（引擎解码**前**的原始字节，自己解码/路由） |

- `SendInputAsync` 要求 `DeserializeSubmitInputResponse` 已配置；返回完整 per-submit 结果，满足需要校验服务端响应的 statesync 客户端（如 shooter 的 `ValidateInput` 要 `ServerTicks`）。
- `RawServerPushReceived` 在引擎类型化解码**之前**触发 —— 想直接喂既有 `(opCode, payload)` apply 管线（如 shooter 的 `ApplyGatewayPush`）的消费者订这个，不要再订 typed 事件（避免重复处理）。
- moba 的既有路径（`SendInput` + typed 事件）完全不变。一个引擎，两种契约形态 —— 即"契约中立"。

## 相关

- 组装根/连接 → `com.abilitykit.network.sdk`
- 房间会话 → `com.abilitykit.network.room`
- 高层配置构建器 → `com.abilitykit.network.battle.config`（`NetworkBattleConfig`：协议预设 + 流式 builder）
- 完整接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
