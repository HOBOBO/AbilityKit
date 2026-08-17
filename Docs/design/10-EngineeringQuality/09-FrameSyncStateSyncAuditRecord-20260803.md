# 帧同步与状态同步审计记录（2026-08-03）

> 文档类型：Historical Audit Record，不是当前稳定设计或发布 gate 清单。
>
> 原始审计日期：2026-08-03；当前事实复核：2026-08-16。
>
> 当前契约以 [帧同步](../07-NetworkSynchronization/01-FrameSync.md)、[状态同步](../07-NetworkSynchronization/02-StateSync.md)、[测试工作流](01-TestingWorkflow.md)、[Beta 发布检查清单](06-BetaStabilizationAndReleaseChecklist.md) 和 [Analysis Artifact 与运行证据](07-AnalysisArtifactAndRuntimeEvidence.md) 为准。

---

## 当前复核摘要

| 原审计结论 | 2026-08-16 状态 | 当前边界 |
| --- | --- | --- |
| 预测类型被错误标记为废弃 | 已修复 | `ClientPredictionRunner` 与 `ClientPredictionReconciler` 仍有活跃消费者 |
| MOBA diagnostics 返回空值 | 已修复 | `MobaBattleRuntimeAdapter.GetWorldDiagnostics` 已输出可用诊断 |
| PureState 使用空引用 `Actors` | 已修复 | 当前返回空列表，PureState 实际状态由 `Payload` 承载 |
| `FrameCommandBuffer._latestFrame` 缺少并发保护 | 已修复 | 当前使用原子读写和 compare-exchange 更新 |
| CatchUp 客户端能力可用于重连 | 已实现但未接入 | `WorldCatchUpDriver` 有真实消费者；`FrameSyncCatchUpClientModule` 仍未安装到客户端 reconnect 主链 |
| MOBA Smoke 已覆盖 FrameSync 模板 | 未形成 gate | Program 支持 `--sync-template`，但现有 smoke 脚本和 `moba-smoke`、`moba-multiprocess` gate 未透传该参数 |
| gate JSON 的同步策略等于真实 CI 覆盖 | 不成立 | `moba-smoke` 有 workflow job；`moba-multiprocess` 没有对应 job，配置意图与实际编排必须分开核对 |
| MOBA multiprocess 仍只有单进程场景 | 部分失效 | runner 已将 host-only Orleans silo 和 client-only 场景放到独立进程；client-only 场景内部仍由一个进程创建 owner/member 两条 TCP 连接，不是两个独立客户端 OS 进程 |
| `SessionLifecycleHost` 需要 Options 重构 | 历史项已失效 | Options 重构已经完成，未稳定的 create/join/restore 聚合入口也已删除 |
| LZ4/Zstd 可作为压缩实现 | 未实现 | 当前明确抛出 `NotSupportedException`，不能作为可用 codec 宣称 |

历史章节中的“已完成”只表示 2026-08-03 当批实施记录。任何当前发布判断都必须重新核验源码、测试、脚本、CI policy 和 artifact，不能从本记录直接推导。

2026-08-16 复核没有把本页改写为 canonical 设计。当前同步语义、客户端策略和恢复边界由 FrameSync/StateSync 设计文档负责；自动化证据由测试工作流负责；发布缺口由 Beta 检查清单负责。本页保留原始问题、当时修复和低优先级 backlog，供追溯决策背景使用。

---

## 原始进度总览

| 批次 | 项目数 | 状态 | 完成日期 |
|------|--------|------|----------|
| 第 1 批（高严重度） | 3 | ✅ 已完成 | 2026-08-03 |
| 第 2 批（中严重度） | 4 | ✅ 已完成 | 2026-08-03 |
| 第 3 批（结构优化） | 5 | ✅ 已完成 | 2026-08-03 |
| 第 4 批（低严重度） | 8 | 🔶 3/8 完成 | 2026-08-03 |

---

## 原始架构背景

以下内容保留 2026-08-03 的审计视角。当前实现中，`BattleFrameSyncGrain` 是按模板启用的权威帧节奏与输入 relay；`BattleLogicHostGrain` 承载玩法运行时和 StateSync 发布。两者可以协作，但不是一条必须串行经过的单一同步管道。

### 两条同步通路

| | MOBA Demo | Shooter Demo |
|---|---|---|
| 主同步模式 | FrameSync (Hybrid) + StateSync 辅助 | 纯 StateSync |
| 服务端世界 | `BattleWorldWithFrameSync`（外部时钟） | `BattleWorld`（自驱动） |
| 客户端预测 | 复用 `ClientPredictionDriverModule` | 自写 `ShooterClientPredictionRuntimeAdapter` |
| ECS 框架 | Entitas | Svelto.ECS |

### 关键发现

1. **三套并行客户端预测** — `world.framesync/Rollback`（主集合）、`host.extension/Client/FrameSync`（Generic 集合）、Shooter 自写 — 互不复用
2. **MOBA hash 对账空转** — `GetWorldDiagnostics` 返回 null → `BattleLogicHostGrain` 每帧用帧号近似 hash
3. **`ClientPredictionRunner`/`ClientPredictionReconciler` 标记 `[Obsolete]`** 但实际有 5 个活跃调用点
4. **`DeltaCompressor` LZ4/Zstd 是假实现** — 内部调用 GZip，且零消费者
5. **`WorldCatchUpDriver` 重复** — 两个完全相同的 `CatchUpAndFeedSnapshots` 实现
6. **`FrameSyncCatchUpClientModule` 零消费者** — 已完整实现但未接入重连流程
7. **双 Gateway 房间准备路径** — `GatewayMultiplayerRoomSession`（大厅）和 `GatewayRoomPreparationController`（战斗层）独立实现 GuestLogin/CreateRoom/JoinRoom
8. **MobaSmoke 未覆盖 FrameSync 路径** — 仅测试 StateSync 模板

---

## 第 1 批：高严重度（✅ 已完成）

### 1.1 移除 `ClientPredictionRunner` / `ClientPredictionReconciler` 的错误 `[Obsolete]`

**问题**：两个类的 `[Obsolete]` 属性声称"no consumer"，但实际有 5 个活跃调用点。

**活跃引用**：

| 类 | 引用文件 | 用途 |
|---|---------|------|
| `ClientPredictionRunner` | `ShooterClientFrameSyncController.cs:65` | Shooter demo 预测 |
| `ClientPredictionRunner` | `ClientPredictionTestHarness.cs:123` | MOBA 测试工具 |
| `ClientPredictionReconciler` | `ClientPredictionDriverModule.cs:571` | 规范帧同步路径 |
| `ClientPredictionReconciler` | `ClientPredictionTestHarness.cs:121` | MOBA 测试工具 |
| `ClientPredictionReconciler` | `ShooterClientFrameSyncController.cs:70` | Shooter demo 对账 |

**已完成变更**：
- `world.framesync/.../ClientPredictionRunner.cs` — 删除 `[Obsolete]`，更新 doc comment
- `world.framesync/.../ClientPredictionReconciler.cs` — 删除 `[Obsolete]`，更新 doc comment

---

### 1.2 接入 MOBA State Hash — 修复 `GetWorldDiagnostics` 返回 null

**问题**：`MobaBattleRuntimeAdapter.GetWorldDiagnostics` 硬编码 `return null`。
`BattleLogicHostGrain:642` 每帧 `stateHash` 回退到 `(uint)_battleHostState.Frame`（帧号近似值）。

**已有基础设施**：
- `MobaDeterministicCheckpointCoordinator.ComputeStateHash(FrameIndex) → uint` — 已实现、已通过 4 个单元测试
- 位置：`com.abilitykit.demo.moba.runtime/Runtime/Application/Services/StateSync/`

**已完成变更**：
- `MobaBattleRuntimeAdapter.cs` — 在 `Start()` 中从 `_battleWorld.Services` 解析 `MobaDeterministicCheckpointCoordinator`，在 `GetWorldDiagnostics()` 中调用 `ComputeStateHash`
- 若 coordinator 未注册则 graceful degradation（使用帧号近似值）

---

### 1.3 清理 `DeltaCompressor` 的假 LZ4/Zstd 方法

**问题**：`DeltaCompressor.cs:89-107` 中 LZ4/Zstd 四个方法内部调用 GZip。零消费者。

**已完成变更**：
- `DeltaCompressor.cs` — 四个假方法改为 `throw new NotSupportedException`，添加类级 doc comment 说明当前状态和未来引入 NuGet 包的计划

---

## 第 2 批：中严重度（✅ 已完成）

### 2.1 标注 `FrameSyncCatchUpClientModule` 待接入

**已完成变更**：
- `FrameSyncCatchUpClientModule.cs` — 添加 doc comment 说明模块已实现但未接入重连流程，计划 v0.2.0 激活

---

### 2.2 删除 `WorldCatchUpDriver` 重复实现

**问题**：`FrameSyncDriverModule.cs:279`（public `WorldCatchUpDriver`）和 `WorldCatchUpDriver.cs:10`（internal `WorldCatchUpDriverInternal`）完全相同的实现。仅前者被 `SessionWorldCatchUpController` 使用。

**已完成变更**：
- **删除** `host.extension/.../FrameSync/WorldCatchUpDriver.cs` + `.meta`（重复实现）

---

### 2.3 修复 `StateSyncPush.Actors` PureState 模式下的 `null!`

**问题**：`ShooterBattleRuntimeAdapter.cs:632` — `Actors = null!`

**已完成变更**：
- `ShooterBattleRuntimeAdapter.cs:632` — `null!` → `new List<WireStateSyncActorSnapshot>()`

---

### 2.4 MobaSmoke 增加 FrameSync 模板参数

**2026-08-03 已完成变更**：
- `MobaSmoke/Program.cs` — 新增 `--sync-template` CLI 参数（默认 `"state-sync-authority"` 保持向后兼容），传入 `RunScenarioAsync` 并用于 `CreateRoomAsync` 的 tags
- 新增 `MobaSmokeConstants.DefaultSyncTemplateId` 常量
- 新增 `ParseStringArgument` 辅助方法

**2026-08-16 复核**：Program 的参数入口仍存在，但 `run_moba_smoke.ps1`、`run_moba_multiprocess_smoke.ps1` 和 `tools/test-gates.json` 没有透传该参数。因此这项只证明 E0/E1 级模板选择入口存在，不证明 FrameSync 模板具备 E4/E5 gate 覆盖。当前 multiprocess runner 已能启动独立 host-only silo 进程和独立 client-only 场景进程，但仍使用默认同步模板。

### 2.5 MOBA multiprocess 当前拓扑与证据边界

2026-08-16 源码复核确认 `run_moba_multiprocess_smoke.ps1` 已不再只是“标准 smoke 加 host-only 存活检查”：

1. Orleans host-only silo 在独立 OS 进程运行；
2. client-only 场景在另一个独立 OS 进程运行；
3. client-only 进程内创建 owner/member 两条 TCP 连接；
4. 场景核对双方快照和移动收敛、显式全量恢复，以及可靠事件 epoch/watermark ACK。

这已经是有效的多进程 E4 场景，但不能扩大成“两个客户端进程”或“FrameSync 模板已覆盖”。`tools/test-gates.json` 中 `moba-multiprocess` 的描述仍把 client-only 写成未来扩展，已经落后于 runner；同时 `.github/workflows/abilitykit-test-gates.yml` 只有 `moba-smoke` job，没有 `moba-multiprocess` job，因此配置项存在不等于 E5 持续执行。

---

## 第 3 批：结构优化（🔶 4/5 完成）

### 3.1 ✅ 双 Gateway 路径添加交叉引用注释

**已完成变更**：
- `GatewayRoomPreparationController.cs` — doc comment 添加 `See also: GatewayMultiplayerRoomSession`
- `GatewayMultiplayerRoomSession.cs` — doc comment 添加 `See also: GatewayRoomPreparationController`

---

### 3.2 ✅ `SessionLifecycleHost` 构造函数引入 Options 对象

**已完成变更**：
- `BattleSessionFeature.OrchestratorHost.cs` — 新建 `SessionLifecycleHostOptions` 类（18 个 `{ get; init; }` 属性），`SessionLifecycleHost` 构造函数改为接受 `SessionLifecycleHostOptions` 参数
- `BattleSessionFeature.cs:80-98` — 调用处改为 `new SessionLifecycleHost(new SessionLifecycleHostOptions { ... })`

---

### 3.3 ✅ 添加三套预测的迁移 TODO

**已完成变更**：
- `ClientPredictionDriverModule.cs` — namespace 下添加 TODO(v1.0) 注释
- `ShooterClientPredictionRuntimeAdapter.cs` — namespace 下添加 TODO(v1.0) 注释

---

### 3.4 ✅ 统一 `PlayerId` 类型别名

**已完成变更**：
- `ExistingGatewayRoomBattleBootstrapper.cs` — 移除 `using ProtocolPlayerId = AbilityKit.Ability.Host.PlayerId;`，两处使用改为全限定名 `new AbilityKit.Ability.Host.PlayerId(...)`

---

### 3.5 ✅ Backlog 文档化

**2026-08-03 已完成变更**：
- `Docs/design/10-EngineeringQuality/06-BetaStabilizationAndReleaseChecklist.md`
  - 更新当时已过时的 `ClientPredictionRunner`/`ClientPredictionReconciler` 废弃声明
  - 增加当时的 v0.1.0 Known Issues Backlog

**2026-08-09 复核**：该 backlog 是历史快照，其中部分项目已经完成或因 API 删除而失效。当前发布风险以 Beta 发布检查清单的“当前复核风险”为准。

---

## 第 4 批：低严重度 Backlog（🔶 3/8 完成）

### 4.1 ✅ `FrameCommandBuffer._latestFrame` 并发保护

**已完成变更**：
- `FrameCommandBuffer.cs` — `LatestFrame` getter 改用 `Volatile.Read`；`SubmitCommand` 中 `_latestFrame` 的 check-then-set 改为 `Interlocked.CompareExchange` 循环；`Clear` 中改用 `Volatile.Write`

### 4.2 ✅ `TrySpawnBotPlayer` 空实现补充说明

**已完成变更**：
- `MobaBattleRuntimeAdapter.cs:299-311` — 添加 TODO(v0.2.0) 注释，详细说明需要通过 MOBA 实体工厂真正创建 Bot 玩家实体的步骤

### 4.3 ✅ `NetworkSyncModel` 枚举预留值文档化

**已完成变更**：
- `NetworkSyncModel.cs` — 类级 doc comment 新增已实现/预留值分类说明，解释预留值存在的原因

### 4.4 ⬜ 剩余低严重度项目

| # | 项目 | 严重度 | 计划版本 | 涉及文件 |
|---|------|--------|----------|----------|
| 1 | 定点数学库 — 浮点 hash 仅检测不修正 | P2 | v1.0 | 全框架 |
| 4 | `OnPostTick` 在预测 stall 时跳过 snapshot capture | P3 | v0.3.0 | `HostRuntime.cs` |
| 5 | `ShooterBattleRuntimePort` 13 个构造器应简化 | P3 | v0.3.0 | `ShooterBattleRuntimePort.cs` |
| 6 | `PlayerInputCommand` 使用 `byte[]` 改用 `ArraySegment<byte>` | P3 | v0.2.0 | `PlayerInputCommand.cs` |
| 7 | 三套客户端预测栈待统一抽象 (`IClientPredictionDriver`) | P2 | v1.0 | 三个包 |

---

## 关键文件索引

### 帧同步核心

| 文件 | 职责 |
|------|------|
| `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/ClientPredictionDriverModule.cs` | 客户端预测驱动（核心，1091 行） |
| `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/FrameSyncDriverModule.cs` | 服务端输入汇聚 + 广播 + `WorldCatchUpDriver` |
| `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/RollbackCoordinator.cs` | 快照捕获/恢复 |
| `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/ClientPredictionRunner.cs` | 单世界预测/回放器（Shooter + MOBA 测试使用） |
| `Unity/Packages/com.abilitykit.world.framesync/Runtime/FrameSync/Rollback/ClientPredictionReconciler.cs` | Hash 对账协调器 |
| `Unity/Packages/com.abilitykit.host.extension/Runtime/Client/FrameSync/CatchUp/FrameSyncCatchUpClientModule.cs` | CatchUp 客户端模块（待接入重连） |
| `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/Sim/RemoteDrivenWorldTickDriver.cs` | MOBA RemoteDriven Tick 驱动 |

### 状态同步核心

| 文件 | 职责 |
|------|------|
| `Unity/Packages/com.abilitykit.world.statesync/Runtime/StateSync/Compression/DeltaCompressor.cs` | 增量压缩器（无消费者） |
| `Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Synchronization/ShooterPackedSnapshotExporter.cs` | Shooter Packed 快照导出 |
| `Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Synchronization/ShooterPureStateSnapshotExporter.cs` | Shooter PureState 快照导出（含 AOI） |
| `Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterClientPredictionRuntimeAdapter.cs` | Shooter 客户端预测适配器 |
| `Server/Orleans/src/AbilityKit.Orleans.Grains/Gameplays/Shooter/Battle/ShooterServerSyncTemplateCatalog.cs` | Shooter 8 种同步模板 |

### 服务端 (Orleans)

| 文件 | 职责 |
|------|------|
| `Server/Orleans/src/AbilityKit.Orleans.Grains/FrameSync/BattleFrameSyncGrain.cs` | 帧时钟 + 输入收集 + 广播 |
| `Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/BattleLogicHostGrain.cs` | 战斗运行时（统一 MOBA + Shooter） |
| `Server/Orleans/src/AbilityKit.Orleans.Grains/Gameplays/Moba/Battle/MobaBattleRuntimeAdapter.cs` | MOBA 运行时适配器 |
| `Server/Orleans/src/AbilityKit.Orleans.Grains/Gameplays/Shooter/Battle/ShooterBattleRuntimeAdapter.cs` | Shooter 运行时适配器 |
| `Server/Orleans/src/AbilityKit.Orleans.Grains/Gameplay/ServerGameplayModuleCatalog.cs` | 玩法模块目录 + sync profile 配置 |
| `Server/Orleans/src/AbilityKit.Orleans.Gateway/Gateway/Core/GatewayFrameSyncSubscriptionManager.cs` | 帧同步推送订阅 |
| `Server/Orleans/src/AbilityKit.Orleans.Gateway/Gateway/Core/GatewayStateSyncPushSubscriptionManager.cs` | 状态同步推送订阅 |

### MOBA Demo 接入

| 文件 | 职责 |
|------|------|
| `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/Core/BattleSessionFeature.cs` | 核心构造器（47 partial 文件） |
| `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/App/Flow/GatewayMultiplayerRoomSession.cs` | 大厅房间会话（路径 B） |
| `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/Gateway/GatewayRoomPreparationController.cs` | 快速房间准备（路径 A） |
| `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Bootstrap/ExistingGatewayRoomBattleBootstrapper.cs` | 大厅→战斗桥接 |
| `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/StateSync/MobaDeterministicCheckpoint.cs` | 确定性检查点 + hash 计算 |

### Smoke 测试

| 文件 | 职责 |
|------|------|
| `Server/Orleans/src/AbilityKit.Orleans.MobaSmoke/Program.cs` | MOBA 端到端烟雾测试 |
| `Server/Orleans/src/AbilityKit.Orleans.ShooterSmoke/Program.cs` | Shooter 端到端烟雾测试 |

### 设计文档

| 文件 | 内容 |
|------|------|
| `Docs/design/07-NetworkSynchronization/01-FrameSync.md` | 当前 FrameSync 稳定设计与实现边界 |
| `Docs/design/07-NetworkSynchronization/02-StateSync.md` | 当前 StateSync 稳定设计与实现边界 |
| `Docs/design/10-EngineeringQuality/01-TestingWorkflow.md` | 当前 gate、CI policy 与验证入口 |
| `Docs/design/10-EngineeringQuality/06-BetaStabilizationAndReleaseChecklist.md` | 当前 Beta 发布检查和风险复核 |
| `Docs/design/10-EngineeringQuality/07-AnalysisArtifactAndRuntimeEvidence.md` | 当前 artifact 与运行证据契约 |
| `Unity/Packages/com.abilitykit.host.extension/Runtime/FrameSync/Design.md` | FrameSync 预测/回滚包内设计 |

---

## 架构图

```
┌──────────────────────────────────────────────────────────┐
│                     服务端 (Orleans)                       │
│                                                          │
│  ┌─────────────────────┐  ┌──────────────────────────┐   │
│  │ BattleFrameSyncGrain │  │ BattleLogicHostGrain      │   │
│  │ - 帧时钟 (30Hz)      │  │ - 双模式: 自驱/外部驱     │   │
│  │ - 输入收集/验证      │─▶│ - world.Tick + 快照输出    │   │
│  │ - 输入历史 (600帧)   │  │ - 可靠事件 + StateSync    │   │
│  │ - CatchUp 请求处理   │  │ - MOBA/Shooter 统一适配    │   │
│  └──────────┬──────────┘  └────────────┬─────────────┘   │
│             │ (IFrameSyncObserver)      │ (IStateSyncObserver) │
│  ┌──────────┴──────────┐  ┌────────────┴─────────────┐   │
│  │ GatewayFrameSync     │  │ GatewayStateSyncPush      │   │
│  │ SubscriptionMgr      │  │ SubscriptionMgr           │   │
│  │ - FramePushed(9001)  │  │ - SnapshotPushed          │   │
│  └──────────┬───────────┘  └────────────┬──────────────┘   │
└─────────────┼──────────────────────────┼──────────────────┘
              │ TCP                      │ TCP
┌─────────────┼──────────────────────────┼──────────────────┐
│             ▼                          ▼                   │
│  ┌──────────────────────┐  ┌──────────────────────────┐   │
│  │ FrameJitterBuffer    │  │ StateSync Decoder         │   │
│  │ → ClientPrediction   │  │ → Actor Snapshot → View   │   │
│  │   DriverModule       │  │   EntityWorld (插值表现)  │   │
│  └──────────────────────┘  └──────────────────────────┘   │
│                     客户端 (Unity)                          │
└──────────────────────────────────────────────────────────┘
```

### MOBA 双 Sim 模式

| Sim | 驱动方式 | 预测 | 使用的 Primitive 集合 |
|-----|----------|------|----------------------|
| ConfirmedAuthority | 权威快照 → `ConfirmedAuthorityWorldTickDriver` | 不预测 | Generic 集合 |
| RemoteDriven | 远程帧 → `RemoteDrivenWorldTickDriver` | 预测+回滚 | 主集合 + `ClientPredictionDriverModule` |

### Shooter 三种客户端同步策略

| 策略 | 预测 | 对账 |
|------|------|------|
| PredictRollback | ✅ PackedSnapshot 回滚 | ✅ Hash mismatch → resync |
| AuthoritativeInterpolation | ❌ 纯插值 | ❌ |
| HybridHeroPrediction | ✅ 仅本地英雄 | ✅ 仅本地英雄 |

---

*历史记录版本：v3.0 | 当前事实复核：2026-08-16 | 原始审计：2026-08-03*
