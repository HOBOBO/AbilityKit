# CatchUp 追帧子系统（v0.1.0 更新 —— 服务端集成完成）

源文件分布：
- 算法层：`Runtime/FrameSync/CatchUp/Shared/` (IFrameSyncInputHistory, FrameSyncCatchUpPolicy, FrameSyncCatchUpTypes, FrameSyncCatchUpMessages)
- 服务端实现：`Runtime/Server/FrameSync/CatchUp/InMemoryFrameSyncInputHistory.cs`
- 客户端实现：`Runtime/Client/FrameSync/CatchUp/WorldStartFrameCatchUpCalculator.cs`
- **🆕 服务端集成**：`Server/Orleans/src/AbilityKit.Orleans.Grains/FrameSync/BattleFrameSyncGrain.cs` — `_inputHistory` (SortedDictionary, 600帧环形缓冲) + `RequestCatchUpAsync`
- **🆕 Gateway Handler**：`CatchUpRequestHandler.cs` — `CatchUpRequest=2002` / `CatchUpPayloadPush=9002`
- **🆕 客户端模块**：`Runtime/Client/FrameSync/CatchUp/FrameSyncCatchUpClientModule.cs` — IHostRuntimeModule, DecideCatchUp / ApplyCatchUpPayload / TryCatchUp
- **🆕 协议层**：`protocol.moba/GatewayFrameSync/CatchUpWireTypes.cs` — WireCatchUpRequest / WireCatchUpFrame / WireCatchUpPayloadPush

## 架构（2026-08-01 更新）

```
客户端重连 / 观战加入
  → 发送 CatchUpRequest(from, to) 到 Gateway (opcode 2002)
  → CatchUpRequestHandler 验证房间 + 战斗状态
  → BattleFrameSyncGrain.RequestCatchUpAsync() 从 _inputHistory ring buffer 提取
  → 返回 CatchUpPayloadPush (opcode 9002) 或 null（历史不完整 → 客户端回退到 FullSnapshot）
  → 客户端 FeedCatchUpPayload → SpectatorWorldDriver 或 ClientPredictionDriverModule 快进
```

## 关键决策

| 决策 | 值 | 说明 |
|------|-----|------|
| MaxHistoryFrames | 600 | 环形缓冲区容量，超出后自动修剪 |
| MaxCatchUpFrames (Policy) | 600 | 超过此 gap 返回 SendSnapshot（回退到全量快照） |
| MaxBatchFrames | 120 | 单批 CatchUp 最大帧数，超过截断 |
| SafetyMarginFrames | 2 | 权威帧的安全余量 |
