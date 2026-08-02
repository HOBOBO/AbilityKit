# MOBA 多人同步完整度（v0.1.0）

基于 2026-08-01 源码核校。覆盖 MOBA demo 帧同步方案从服务端到客户端的完整能力矩阵。

## 同步链路概览

```
客户端输入 → Gateway(SubmitFrameInput) → BattleFrameSyncGrain(收集+广播)
  → GatewayFrameSyncSubscriptionManager(推送到各连接)
  → FramePacketNetAdapter → FrameJitterBuffer → ClientPredictionDriverModule
  → RemoteDrivenWorld.Tick + ConfirmedAuthorityWorld.Tick
```

## 能力矩阵

| 能力 | 状态 | 关键文件 |
|------|------|---------|
| 帧输入采集+上行 | ✅ | `SubmitFrameInputHandler.cs`, `BattleLocalInputQueue.cs` |
| 服务端帧中继 (FrameRelayOnly) | ✅ | `BattleFrameSyncGrain.OnTickAsync` |
| **🆕 服务端权威世界 (BattleWorldWithFrameSync)** | ✅ v0.1.0 | `BattleLogicHostGrain.TickFrameAsync` + `ServerGameplayModuleCatalog.BattleWorldWithFrameSync` |
| 客户端预测+回滚 | ✅ | `ClientPredictionDriverModule.cs` (1091行) |
| Hash 对账 (Reconcile) | ✅ | `ClientPredictionReconciler` + `IClientPredictionReconcileControl` |
| **🆕 CatchUp 追帧** | ✅ v0.1.0 | `BattleFrameSyncGrain._inputHistory` + `CatchUpRequestHandler` |
| **🆕 帧录制/回放** | ✅ v0.1.0 | `BattleFrameSyncGrain.DumpRecordingAsync` + `FrameSyncRecording` |
| **🆕 结构化健康监控** | ✅ v0.1.0 | `GetFrameSyncMetricsHandler` + `FrameSyncMetrics` (12字段) |
| **🆕 观战模式** | ✅ v0.1.0 | `SpectatorWorldDriver` + `SpectatorSubscribeHandler` + `BattleSessionFeature.Spectator` |
| **🆕 服务端 Bot AI** | ✅ v0.1.0 | `MobaBattleRuntimeSession.MountBotAi` + 随机移动输入生成 |
| **🆕 动态 Tick Rate** | ✅ v0.1.0 | `BattleFrameSyncGrain.AdjustTickRateAsync [10,60]Hz` |
| **🆕 重连 ReconcileEnabled 恢复** | ✅ v0.1.0 | `BattleSessionFeature.Reconnect.SetReconcileEnabled(true)` |
| 网络条件模拟 | ✅ 已有 | `NetworkConditionController` + 6 预设档案 |
| 确定性随机数 | ✅ | `RollbackWorldRandom` (xorshift32) |
| 确定性检查点 | ✅ | `MobaDeterministicCheckpoint` + `MobaStateHashBuilder` |

## 同步模式切换

```csharp
// FrameRelayOnly (旧默认): 纯帧中继
ServerBattleSyncProfile.FrameSync("frame-sync-authority", "state-sync-authority")
// → RequiresBattleRuntime = false

// BattleWorldWithFrameSync (新默认, v0.1.0): 帧中继 + 权威世界推进
ServerBattleSyncProfile.FrameSync("frame-sync-authority", ["state-sync-authority"],
    ServerBattleRuntimeMode.BattleWorldWithFrameSync)
// → RequiresBattleRuntime = true, 同时创建 BattleFrameSyncGrain + BattleLogicHostGrain
```

## Gateway FrameSync 协议 (protocol.moba)

| OpCode | 名称 | 说明 |
|--------|------|------|
| 2001 | SubmitFrameInput | 客户端提交帧输入 |
| 2002 | CatchUpRequest | 请求追帧输入历史 |
| 2003 | GetMetricsRequest | 查询运行时指标 |
| 2004 | SpectatorSubscribe | 观战者订阅 |
| 9001 | FramePushed | 帧输入广播 |
| 9002 | CatchUpPayloadPush | 追帧数据推送 |
| 9003 | MetricsResponse | 指标响应 |

## 已知待补项

| 项目 | 优先级 | 说明 |
|------|--------|------|
| 定点数学库 | P2 | 延后至框架稳定 (v1.0)，浮点 hash 对账可检测但无法修正 |
| Host Migration | P2 | 依赖 Orleans 集群能力 |
| MOBA state hash 接入 GetWorldDiagnostics | P1 | 当前 `MobaBattleRuntimeAdapter` 返回 null，用帧号近似 |
