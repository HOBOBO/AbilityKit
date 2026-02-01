---
name: framesync-prediction-rollback
section: key_files
---

# Key files

## Driver / Integration

- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs`
  - per-world `WorldContext`
  - confirmed/predicted 推进
  - prediction window（EWMA backlog）
  - idealFrame window cap + stall 归因
  - rollback/replay timeout + reconcile stats

- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/IClientPredictionDriverStats.cs`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/IClientPredictionTuningControl.cs`
- `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/IClientPredictionReconcileTarget.cs`

## Rollback primitives

- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackCoordinator.cs`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackSnapshotRingBuffer.cs`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackRegistry.cs`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/IRollbackStateProvider.cs`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/InputHistoryRingBuffer.cs`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/WorldStateHashRingBuffer.cs`
- `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/ClientPredictionReconciler.cs`

## Time sync / ideal frame (example implementation)

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Flow/Battle/BattleSessionFeature.cs`
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Flow/Battle/BattleFlowDebugProvider.cs`
- `Unity/Packages/com.abilitykit.demo.moba.editor/Editor/BattleDebug/Panels/BattleDebugFrameSyncTimePanel.cs`
- `Unity/Packages/com.abilitykit.demo.moba.editor/Editor/BattleDebug/Panels/BattleDebugFrameSyncPredictionPanel.cs`
