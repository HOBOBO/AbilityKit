# 线协议（protocol.shooter）

包：`com.abilitykit.protocol.shooter`，7 个 .cs 文件，全部 `[MemoryPackable]` + `WireSerializer`。

## Opcode 表（ShooterOpCodes）

`Runtime/ShooterOpCodes.cs`：

| Opcode | 值 | 用途 |
|--------|---|------|
| `Input.PlayerCommand` | **5101** | 玩家输入命令 |
| `Snapshot.StartGame` | **5201** | 开战 |
| `Snapshot.State` | **5202** | 全量状态快照 |
| `Snapshot.Events` | **5203** | 事件 |
| `Snapshot.PackedState` | **5204** | Packed 全量 |
| `Snapshot.PackedStateDelta` | **5205** | Packed 增量 |
| `Snapshot.StateHash` | **5206** | 状态 hash |
| `Snapshot.PureState` | **5207** | Pure baseline |
| `Snapshot.PureStateDelta` | **5208** | Pure delta |

## Input Codec

`Runtime/Input/ShooterInputCodec.cs`：

```csharp
[MemoryPackable]
public struct ShooterPlayerCommand {
    public int PlayerId;
    public float MoveX, MoveY;
    public float AimX, AimY;
    public bool Fire;
    public ShooterPlayerAttackSlots AttackSlot;   // Primary=0 / Spread=1 / Twin=2
}

public enum ShooterPlayerAttackSlots { Primary=0, Spread=1, Twin=2 }

[MemoryPackable]
public struct ShooterInputPayload { ... }
```

## StartGame Codec

`Runtime/StartGame/ShooterStartGameCodec.cs`：开战载荷。

## StateSync Compatibility Policy

`Runtime/StateSync/ShooterStateSyncCompatibilityPolicy.cs`：版本兼容性策略（客户端旧版本如何处理）。

## Codec 实现

- `ShooterStateSnapshotCodec.cs`（全量）
- `ShooterPackedSnapshotCodec.cs`（packed chunk + delta despawned）
- `ShooterPureStateSyncCodec.cs`（pure + AOI + 定点量化）

## 网络流程

### 客户端上行

```
玩家输入 → ShooterPlayerCommand(opcode 5101) → Gateway → RoomGrain
    → ShooterBattleRuntimeAdapter.SubmitInput
    → ShooterBattleState.InputBuffer
```

### 服务端下行

```
ShooterBattleRuntimePort.Tick
    → ShooterBattleSveltoStepEngine.Step（推进 Svelto EnginesRoot）
    → 三类快照导出
        ├─ 全量 ShooterStateSnapshotPayload (5202)
        ├─ PackedSnapshot (5204/5205 delta)
        └─ PureStateSnapshot (5207/5208 baseline/delta)
    → Gateway StateSyncPush 下推
    → 客户端 ShooterClientSnapshotApplyCoordinator 应用
    → ShooterSnapshotViewProjection → ShooterSnapshotViewBinder / ShooterDotsSnapshotViewBinder
```

### 状态 hash 校验

```
服务端 ComputeStateHash (5206) → 客户端对比本地 hash
    → mismatch → ShooterClientResyncReason.AuthoritativeHashMismatch
    → 触发 Drift Recovery
```
