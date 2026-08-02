# 观战模式（v0.1.0 新增）

源文件：
- 框架层：`Runtime/Client/FrameSync/Spectator/SpectatorWorldDriver.cs`
- 协议层：`protocol.moba/GatewayFrameSync/` (OpCodes.SpectatorSubscribe, WireSpectatorSubscribeRes)
- Gateway Handler：`SpectatorSubscribeHandler.cs`
- Demo 集成：`com.abilitykit.demo.moba.view.runtime/.../BattleSessionFeature.Spectator.cs`

## 架构

```
┌─ 框架层 (host.extension) ─────────────────────────────┐
│  SpectatorWorldDriver                                    │
│  - Initialize(hostRuntime, worldId, tickRate, factory)   │
│  - FeedFrameInputs(frame, PlayerInputCommand[])          │
│  - FeedCatchUpPayload(FrameSyncCatchUpPayload)           │
│  - TryTick() → bool                                      │
│  - CatchUpTo(targetFrame, stepsBudget) → int             │
│                                                          │
│  内部: FrameJitterBuffer (delay=0, FillDefault)          │
│       + IWorldInputSink.Submit + IWorld.Tick             │
│  不提交输入、不做预测、不对账                              │
└──────────────────────────────────────────────────────────┘

┌─ Demo 层 (view.runtime) ───────────────────────────────┐
│  BattleSessionFeature.Spectator (partial)                │
│  - TryStartSpectating(INetworkClient, roomId, factory)   │
│  - StopSpectating()                                      │
│  - UpdateSpectatorWorld(stepsBudget)                     │
│                                                          │
│  专用 INetworkClient → 独立 transport，不污染主战斗流程    │
│  零开销：未调用 TryStartSpectating 时 SpectatorDriver=null │
└──────────────────────────────────────────────────────────┘
```

## 观战者加入流程

1. 创建独立的 `INetworkClient` 连接到 Gateway
2. 发送 `SpectatorSubscribe(roomId)` → 获得 (WorldId, TickRate, CurrentFrame)
3. 用 `worldFactory` 创建确定性世界（与正常客户端相同的蓝图）
4. `CatchUpRequest(0, currentFrame)` → 收到 `CatchUpPayloadPush` → `FeedCatchUpPayload`
5. `CatchUpTo(currentFrame)` 批量快进
6. 注册 `OnServerPush` 处理器：`FramePushed` → `FeedFrameInputs` + 每帧 `TryTick`
7. 渲染端从 `SpectatorWorld` 读取实体状态

## 设计决策

- **独立 transport**：不共享主战斗的 NetworkTransport，避免修改现有 StateSyncAdapter / NetworkTransportOptions
- **CatchUp 复用**：观战加入同一房间时复用 P0-2 的 CatchUp 机制追帧到当前帧
- **部分类隔离**：所有观战代码在一个 `BattleSessionFeature.Spectator.cs` 文件中
- **可复用**：`SpectatorWorldDriver` 是框架组件，任何游戏均可使用
