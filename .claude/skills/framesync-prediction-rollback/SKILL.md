---
name: framesync-prediction-rollback
description: AbilityKit 客户端/服务端帧同步的预测、回滚、对账、CatchUp、BattleHost 排查与调试工作流。覆盖 ClientPredictionDriverModule（含新 IClientPredictionReconcileControl）、RollbackCoordinator、两套并行的客户端预测 primitives（world.framesync 与 host.extension/Client/FrameSync）、ServerRollbackModule、FrameSyncDriverModule、CatchUp 子系统。触发场景：预测不前进、predicted 卡住、rollback 不触发或风暴、reconcile mismatch、idealFrame/stall、per-world 统计混乱、CatchUp、ConfirmedAuthority、BattleHost 生命周期。
---

# framesync-prediction-rollback

基于当前源码核校（2026-07-20）。客户端帧同步（预测 + 回滚 + 对账 + CatchUp）速查与工作流。

## 适用场景

- 预测推进不工作 / 卡住（`predicted` 不前进）
- rollback 未触发或触发风暴（输入差异 rollback、hash reconcile）
- reconcile mismatch 不出现或帧号不对齐
- idealFrame gate 导致 stalls 或 prediction window 被压缩
- 多 world 场景下统计混在一起、无法定位
- 服务端回滚（`ServerRollbackModule`）或服务端驱动（`FrameSyncDriverModule`）问题
- CatchUp 子系统（`FrameSyncCatchUpPolicy`、`WorldStartFrameCatchUpCalculator`）

## 关键架构事实

### 两套并行的客户端预测 primitives（重要）

| 集合 | 位置 | 使用方 |
|------|------|--------|
| 主集合 | `com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/`（15 个 .cs） | `ClientPredictionDriverModule`（标准客户端预测） |
| Generic 集合 | `com.abilitykit.host.extension/Runtime/Client/FrameSync/`（4 个 .cs） | `ConfirmedAuthorityWorldRuntimeFactory`（确认权威路径） |

两者**不要混用**。详见 [key_files.md](key_files.md)。

### ClientPredictionDriverModule 实现了 4 个接口（旧 skill 只知 3 个）

```csharp
public sealed class ClientPredictionDriverModule :
    IHostRuntimeModule,
    IClientPredictionDriverStats,
    IClientPredictionTuningControl,
    IClientPredictionReconcileTarget,
    IClientPredictionReconcileControl   // ← 新增，旧 skill 未覆盖
```

新接口 `IClientPredictionReconcileControl` 提供 `SetReconcileEnabled / TryGetReconcileEnabled / ResetReconcile`，Reconcile 调试面板有按钮调用。

### 关键 API 字段名修正

旧 skill 写的 `IdealFrameStalls` **实际叫** `TotalIdealFrameStalls`（long）。另有 `IsPredictionStalledByIdealFrame` / `IsPredictionStalledByWindow`（bool）和 per-world 读取 `TryGetIdealFrameStallStats` / `TryGetPredictionWindowStats`。

### `RecordPredictedHash` 在 ClientPredictionReconciler 上

旧 skill 暗示在 Driver 上。实际：Driver 在 `OnPostTick` 中通过 `ctx.Reconciler.RecordPredictedHash(...)` 调用。`ClientPredictionReconciler` 类位置：`com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/ClientPredictionReconciler.cs`。

## Sections

- [when_to_use.md](when_to_use.md) — 何时启用本 skill（含服务端路径）
- [required_context.md](required_context.md) — 改动前必须明确的上下文
- [key_files.md](key_files.md) — Driver、Rollback primitives（两套）、CatchUp、服务端模块、Editor 面板（6 个）
- [procedure.md](procedure.md) — 排查步骤（含正确字段名）
- [examples_and_troubleshooting.md](examples_and_troubleshooting.md) — 典型故障速查

## 相关 skill

- 完整技能/触发/BUFF 模块速查见 [ability-kit](../ability-kit/SKILL.md)。
- Session/Flow 类代码重构（State/Handles/Controllers）见 [state-handles-controllers](../state-handles-controllers/SKILL.md)。
