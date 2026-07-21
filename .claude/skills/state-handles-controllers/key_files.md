# Key files (current, verified)

> 所有路径基于 2026-07-20 实测存在。主例：Unity 版 `BattleSessionFeature`。

## 主例根目录

`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/`

（旧 skill 的 `Runtime/Game/Flow/Battle/Features/Session/` 完全失效）

## Core/ — 主类与核心三件套

- `Core/BattleSessionFeature.cs` — 主类，sealed partial class，实现 ~15 个 `IBattleSessionFeature / ISession*Runtime / ISession*Host` 接口
- `Core/BattleSessionState.cs` — 纯数据，含嵌套 POCO：`TickState / RemoteDrivenSimState / ConfirmedSimState / FlagsState / GatewayRoomTimeSyncState / EditorHooksState`，每个有 `Reset()`
- `Core/BattleSessionHandles.cs` — 资源持有者，含 `BattleLogicSession Session` + 8 个领域 handles
- `Core/BattleSessionFeature.Runtime.cs` — `ISession*Runtime` 接口声明 + 显式实现（Runtime 契约）
- `Core/BattleSessionFeature.HostBridges.cs` — Host 接口显式实现（反向桥接）
- `Core/BattleSessionFeature.HostInterfaces.cs` — `ITickLoopHost / ISessionOrchestratorHost / INetAdapterContextHost` 等接口定义
- `Core/BattleSessionFeature.Lifecycle.cs` — `OnAttach / OnDetach / Tick`
- `Core/BattleSessionFeature.SessionStart.cs` / `.World.cs`
- `Core/BattleSessionFeature.SubFeatureSetup.cs` / `.SubFeaturePipeline.cs`
- `Core/BattleSessionFeature.Accessors.cs` / `.PhaseAccessors.cs` / `.StateAccessors.cs` / `.SnapshotAccessors.cs` / `.NetworkAccessors.cs`
- `Core/BattleSessionFeature.OrchestratorHost.cs` / `.NetAdapterContextHost.cs` / `.RuntimeContracts.cs`
- `Core/BattleSessionFeature.DispatcherDispose.cs` — dispose helpers 按领域拆

## Handles partial（8 个领域）

- `Core/BattleSessionHandles.cs`（主）
- `Core/BattleSessionHandles.Phase.cs`
- `Core/BattleSessionHandles.Net.cs`
- `Core/BattleSessionHandles.Confirmed.cs`
- `Core/BattleSessionHandles.RemoteDriven.cs`
- `Core/BattleSessionHandles.Snapshot.cs`
- `Core/BattleSessionHandles.GatewayRoom.cs`
- `Core/BattleSessionHandles.Dispatchers.cs`

## Controllers/（10 个 Session*Controller，**不是 BattleSession*Controller**）

- `Controllers/SessionOrchestrator.cs` — `(state, handles, ISessionOrchestratorHost host)`
- `Controllers/TickLoopController.cs` — `(state, handles, ITickLoopHost host)`，`MainTick` 调 `_host.TickRemoteDrivenLocalSim(...)`
- `Controllers/SessionDispatchersController.cs`
- `Controllers/SessionNetAdapterController.cs`
- `Controllers/SessionReplayController.cs`
- `Controllers/SessionPlanController.cs`
- `Controllers/SessionEventsController.cs`
- `Controllers/SessionSnapshotRoutingController.cs`
- `Controllers/SessionWorldCatchUpController.cs`

Controller 是无状态的，所有状态读写都通过 `_state.*` / `_handles.*`。

## SubFeatures/（13 个文件，10 个 SubFeature）

- `SubFeatures/SessionSubFeatures.cs` — 9 个细粒度接口定义（`ISessionLifecycleSubFeature / ISessionMainTickSubFeature / ...`）
- `SubFeatures/SessionSubFeaturePipeline.cs` — 注册流水线
  - `AddStandardSessionSubFeatures`：8 个（Events / GatewayRoom / SnapshotRouting / Dispatchers / EditorHooks / Lifecycle / NetAdapter / Replay）
  - `AddLateSessionSubFeatures`：2 个（TickLoop / Plan）
- `SubFeatures/SessionLifecycleSubFeature.cs` 等

每个 SubFeature 通过 `FeatureModuleContext<BattleSessionFeature>` 反向访问 Feature（经 Runtime 契约，不直接 `feature.Xxx`）。

## Sim/ — 按变体拆 partial（旧 skill 点名属实）

- `Sim/BattleSessionFeature.SimTick.RemoteDriven.cs` — `TickRemoteDrivenLocalSim(float dt)`
- `Sim/BattleSessionFeature.SimTick.Confirmed.cs` — `TickConfirmedAuthorityWorldSim(float dt)`
- `Sim/BattleSessionFeature.RemoteDrivenLocalSim.cs`
- `Sim/BattleSessionFeature.ConfirmedAuthorityWorldSim.cs`
- `Sim/BattleSessionFeature.SimDispose.cs` — dispose helpers 按领域拆

## Gateway/ — 按职责拆

- `Gateway/BattleSessionFeature.GatewayConnection.cs`
- `Gateway/BattleSessionFeature.GatewayPreparation.cs`
- `Gateway/BattleSessionFeature.GatewayTimeSync.cs`
- `Gateway/BattleSessionFeature.GatewayTimeSyncStats.cs`
- `Gateway/BattleSessionFeature.GatewayFrameTiming.cs`
- `Gateway/BattleSessionFeature.GatewayRoom.cs`

## Net/ Snapshot/ Editor/

- `Net/BattleSessionFeature.Net*.cs`
- `Snapshot/BattleSessionFeature.Snapshot*.cs`
- `Editor/BattleSessionFeature.Editor*.cs`（`#if UNITY_EDITOR`）

## WorldSystemBase（Entitas ECS 层，与 Session 层并存）

- `Unity/Packages/com.abilitykit.world.entitas/Runtime/World/Base/WorldSystemBase.cs` — 抽象基类（`OnInit / OnExecute / OnCleanup / OnTearDown`）
- `Unity/Packages/com.abilitykit.world.entitas/Runtime/World/Base/ReactiveWorldSystemBase.cs` — 响应式基类（`OnCleanup` 空实现，资源释放在 `OnTearDown`）

真实子类例子：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Skill/MobaPassiveSkillTriggerRegisterSystem.cs` — `OnTearDown` 遍历 group 反注册被动技能 + 取消订阅
- `Unity/Packages/com.abilitykit.combat.projectile/Runtime/Projectile/Systems/ProjectileTickSystem.cs` — 纯 `OnExecute`，无 `OnTearDown`（多数系统这样）

## Console Demo 简化镜像（**不推荐作主例**）

- `src/AbilityKit.Demo.Moba.Console/Battle/Session/ConsoleSessionState.cs`（注释"对齐 Unity BattleSessionState"）
- `src/AbilityKit.Demo.Moba.Console/Battle/Session/ConsoleSessionHooks.cs`（注释"对齐 Unity BattleSessionHandles"）
- `src/AbilityKit.Demo.Moba.Console/Battle/Session/ConsoleSessionOrchestrator.cs`（注释"对齐 Unity SessionOrchestrator"）

只有 3 个类，没有 Controllers / SubFeatures / partial 拆分，无法演示 skill 要讲的核心准则。可作为"最小可用 Session"的对比参考。
