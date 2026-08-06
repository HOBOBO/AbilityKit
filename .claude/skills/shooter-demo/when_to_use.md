# When to use

启用本 skill 的典型场景：

## 网络同步实现

- 你要实现**服务端权威 StateSync**（区别于 moba 的 FrameSync）
- 你要写**packed/pure-state 快照编码**（chunk 划分、delta despawned、AOI 兴趣裁剪、定点量化）
- 你要做 **Lag Compensation** / **Fast Reconnect** / **Drift Recovery**
- 你要做 **AOI 兴趣管理**（基于 `AbilityKit.Ability.StateSync.Aoi`）

## 客户端同步策略

- 你要选**客户端同步策略**（PredictRollback / AuthoritativeInterpolation / HybridHeroPrediction）
- 你要写**自定义客户端预测回滚**（不复用 moba 的 `ClientPredictionDriverModule`）
- 你要追踪 `ShooterClientRecoveryCoordinator` 状态机（Normal/CatchUp/AwaitingFullSnapshot/ApplyingFullSnapshot/Recovered）

## Svelto ECS

- 你要用 **Svelto.ECS**（区别于 Entitas）写战斗模拟
- 你要写 struct `IEntityComponent` + `ExclusiveGroup` + `EnginesRoot`

## 表现层

- 你要写 **PresentationSession + ViewProjection + ViewBinder**
- 你要做 **DotsSnapshotViewBinder**（Unity DOTS 渲染）
- 你要发 **ViewEvent**（Hit/Fire/MatchVictory/Defeat/Ended）

## 编辑器与 PlayMode

- 你要进 `Tools/AbilityKit/Shooter Demo` 窗口（3 种 DriveMode）
- 你要写 **FrameRecordReplay**（离线回放）
- 你要起**远程 StateSync 服务器**（`restart_shooter_state_sync.bat`）

## 验收与 AI

- 你要写 `ShooterAcceptanceSpecs` 纯 C# 验收（不依赖 Unity）
- 你要跑 `ShooterDeterminismSpecRunner`（确定性验证）
- 你要做 **ML-Agents 训练**（`com.abilitykit.demo.shooter.ai`）

## 不要在本 skill 找的内容

- 技能/BUFF/触发器业务（**shooter 不复用**）→ [ability-kit](../ability-kit/SKILL.md)
- 客户端预测 `ClientPredictionDriverModule`（**shooter 不复用**）→ [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- 多人联网接入（`NetworkSdkBuilder` → `NetworkSdkClient` → `RoomGatewaySessionFlow`，shooter 真实路径，不经 coordinator）→ [coordinator/integration_recipes.md](../coordinator/integration_recipes.md)
- Entitas ECS / 帧同步 demo → [moba-demo](../moba-demo/SKILL.md)
