# 三类快照 + StateHasher + AOI

位置：`runtime/Runtime/Application/Synchronization/`

## 三类快照模型

### 1. 全量 StateSnapshot（opcode 5202）

`ShooterStateSnapshotExporter.cs`：导出 `ShooterStateSnapshotPayload`（含所有玩家、子弹、敌人、事件）。

`ShooterStateSnapshotPayload`（`protocol.shooter/Runtime/StateSync/ShooterStateSnapshotCodec.cs`）：

```csharp
public struct ShooterPlayerSnapshot { PlayerId/X/Y/AimX/AimY/Hp/Score/Alive }
public struct ShooterBulletSnapshot { BulletId/Owner/X/Y/Vx/Vy/RemainingFrames/Penetration/ExplosionRadius/ExplosionDamage }
public struct ShooterEnemySnapshot { EnemyId/X/Y/Hp/... }
public struct ShooterEventSnapshot { Type/... }   // Hit/Fire/MatchVictory/MatchDefeat/MatchEnded
public struct ShooterStateSnapshotPayload { Frame/Players[]/Bullets[]/Enemies[]/Events[] }
```

### 2. PackedSnapshot（opcode 5204 全量 / 5205 delta）

`ShooterPackedSnapshotExporter.cs`：按 component chunk 导出 11 类：

```
RuntimeMetadata / PlayerLifecycle / ProjectileLifecycle / EnemyLifecycle
Transform × 3（Player/Bullet/Enemy）
Health × 2（Player/Enemy）
Score / ProjectileLifetime
```

含 despawned 增量：`_lastExportedProjectileIds / _lastExportedEnemyIds` 追踪 → 生成 despawned 列表。

### 3. PureStateSnapshot（opcode 5207 baseline / 5208 delta）

`ShooterPureStateSnapshotExporter.cs`：pure-state（仅位置/速度等连续状态）baseline/delta + AOI 兴趣筛选。

**定点量化**：位置/速度 `PositionScale = VelocityScale = 1000`（定点小数）。

## ShooterStateHasher（opcode 5206）

`ShooterStateHasher.cs`：`Compute()` 计算当前 world 的 `WorldStateHash`，用于客户端校验。

客户端 hash mismatch → 触发 `ShooterClientResyncReason.AuthoritativeHashMismatch` 或 `ClientHashRejectedByServer` → 重同步。

## ShooterSnapshotOrderBuffer

`ShooterSnapshotOrderBuffer.cs`：保证导出顺序确定（player/projectile/enemy 各自 sorted order）。

## ShooterPureStateInterestPolicy（AOI 兴趣裁剪）

`ShooterPureStateInterestPolicy.cs` + `AbilityKit.Ability.StateSync.Aoi`（`AoiInterestSet` / `AoiSampleBufferView`）：

- pure-state 快照按客户端兴趣裁剪
- AOI 基于客户端观察位置 + 视野半径
- 显著降低带宽（远端实体不下发）

## 相关 Codec

`protocol.shooter/Runtime/`：

- `StateSync/ShooterStateSnapshotCodec.cs`（全量）
- `StateSync/ShooterPackedSnapshotCodec.cs`（packed chunk）
- `PureStateSync/ShooterPureStateSyncCodec.cs`（pure + AOI）
- `StateSync/ShooterStateSyncCompatibilityPolicy.cs`（版本兼容）

## Runtime 工具

- `ShooterRuntimeSnapshotUtility`
- `ShooterDeterminismSpecRunner`（确定性验证）
- `ShooterLagCompensationService`（详见 client_sync.md）
