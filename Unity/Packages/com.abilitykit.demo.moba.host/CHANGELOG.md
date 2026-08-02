# Changelog — com.abilitykit.demo.moba.host

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。本包是从 `com.abilitykit.host.extension` 提取出来的 MOBA 专属 host/session adapter 层（38 个文件、3 个 asmdef），承载 MOBA demo 的战斗启动、房间同步、快照映射、游戏开始协调等宿主逻辑。该提取使 `host.extension` 达到包中立。

### API 边界
- 战斗启动：`MobaBattleLaunchSpec` / `MobaBattleStartPlan` / `MobaGameStartSpec`。
- 主机创建：`MobaHostCreateWorldSpec` / `MobaHostRuntimeBuilder`。
- 房间协调：`MobaRoomOrchestrator` / `MobaRoomState` / `MobaRoomSyncServer`。
- 游戏开始来源：`MatchmakingGameStartSource` / `RoomGameStartSource` / `DungeonPresetGameStartSource`。
- 快照映射：`MobaRuntimeSnapshotMapper`。

### 构建门槛
- 在 `src/AbilityKit.Demo.Moba.Host` 上启用 `AbilityKitStable=true`：`TreatWarningsAsErrors` 已开启，零错误。

### 变更
- 由 `0.0.1` 提升为 `0.1.0`（Beta）。
- 新增 `src/AbilityKit.Demo.Moba.Host.Tests`（首批直接单测，4 用例）。
- 建立 CHANGELOG 与发版基线。
