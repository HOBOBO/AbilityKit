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
| **🆕 房主在线保持 + 掉线迁移** | ✅ 2026-08-04 | `RoomStateMachine.ResolveOwner`：Join/MarkOffline 时若 Owner 是成员但离线，自动把 Owner 交给最早的在线成员（无在线成员则保留以便重连）。消除"孤儿房间"——加入无主房间者会成为房主 |
| **🆕 客户端自动建房** | ✅ 2026-08-04 | `FormalLobbyFeature.TryStartAutomaticCreate` + `BattleGatewayConfigSO.AutoCreateWhenEmpty`(默认 true)：房间列表为空时首个客户端自动建房当房主 |
| **🆕 房间列表自动刷新** | ✅ 2026-08-04 | `FormalLobbyFeature.TryRefreshRoomsAutomatically`：大厅浏览态每 3s 自动 Refresh，客人端可及时看到房主新建的房间 |
| **🆕 无主房间逃生** | ✅ 2026-08-04 | `FormalLobbyFeature.IsOwnerAbsent` + `LeaveAndCreateRoomAsync`：房主离线/缺席时提示并提供"Leave & Create Room"，不再被"等待房主 start"卡死 |

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
| ~~定点数学库~~ | ~~P2~~ | **已完成（2026-08，P0-P4）**：`com.abilitykit.deterministic` 定点栈已全仓接入（弹丸/碰撞/运动/伤害资源/FrameTime/EffectContainer），HP/资源回滚快照存 raw long，state hash 以 HP raw 对账。接入约定见 `com.abilitykit.world.framesync/Document/定点帧同步接入指南.md` |
| Host Migration（战斗级） | P2 | 房间级 Owner 在线保持/掉线迁移已实现（见能力矩阵）；战斗运行时的 Host 迁移仍依赖 Orleans 集群能力 |
| MOBA state hash 接入 GetWorldDiagnostics | P1 | 当前 `MobaBattleRuntimeAdapter` 返回 null，用帧号近似 |
| 跨运行时 JSON float 解析 | P3 | 服务端 .NET 与客户端 Mono/IL2CPP 的配置 float 解析存在理论末位差；当前服务端权威+同构客户端不触发，纯跨运行时 P2P 锁步前需评估 decimal-string 配置通道 |

## E2E 验证（2026-08-03）

| 测试 | 命令 | 结果 |
|------|------|------|
| MOBA 多人烟雾测试 | `run_moba_multiprocess_smoke.ps1 -Configuration Release` | ✅ **PASS** |
| Shooter 多人烟雾测试 | `run_shooter_multiprocess_smoke.ps1 -Configuration Release` | ✅ **PASS** |

**MOBA smoke 结果**：
```
MOBA_SMOKE_PASSED  Players=2  Phase=3  Revision=10
AUTHORITATIVE_INPUT_VERIFIED: Owner moved (-12,0)→(-11.833,0), OwnerPushes=3, MemberPushes=3
RECOVERY_VERIFIED: FullSnapshots=2, Actors=2, EventEpoch verified
```

**修复的阻塞 bug**：
- `battle_maps.json` spawn point ID 不匹配：smoke 测试用 `spawnPointId: 1,2`，配置中实际 ID 为 `101,201`
- `MagnitudeSource.cs:854` `StackingModifier(float)` 编译错误 → `FixedModifier()`

## 统一网关抽象（v0.1.0 新增）

`com.abilitykit.host.extension/Runtime/Gateway/` 新增 `IGatewayConnection` + `GatewayConnection` 统一请求/响应 + 推送注册接口，包装 `IConnection` + seq 匹配，供 MOBA 和 Shooter 两个示例共用。
