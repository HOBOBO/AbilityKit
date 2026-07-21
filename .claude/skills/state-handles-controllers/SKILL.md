---
name: state-handles-controllers
description: AbilityKit Session/Flow 业务代码（如 BattleSessionFeature）重构准则——State(纯数据)/Handles(可释放资源,partial)/Controllers(行为,Session*Controller)/SubFeatures(薄胶水) 分离，含 Host 接口反向桥接、Runtime 契约、partial 按领域拆、WorldSystemBase.OnTearDown 等模式。触发场景：会话/流程类代码臃肿、feature 持续变大、字段/资源/编排/逻辑混在一个类、SubFeature 越权访问、BattleSessionFeature 拆分、Start/Stop 顺序、State 混入 IDisposable、OnCleanup vs OnTearDown。
---

# state-handles-controllers

基于当前源码核校（2026-07-20）。本 skill 用于把"会话/流程类代码"重构为：

- **`State`**：纯数据（POCO，可含嵌套子状态 + 各自 `Reset()`）
- **`Handles`**：可释放资源/引用（自身也常是 partial，按领域拆）
- **`Controllers`**：行为逻辑（无状态，签名 `(state, handles, host)`）
- **`SubFeatures`**：薄胶水（实现细粒度 `ISession*SubFeature` 接口）

并补齐必要的中文注释（文件头说明 + 行内注释中文）。

## 当前真实主例：BattleSessionFeature（Unity 版）

**位置**：`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/Core/BattleSessionFeature.cs`

注意：Console 版（`src/AbilityKit.Demo.Moba.Console/Battle/Session/Console*`）是 Unity 版的**轻量镜像**（注释明说"对齐 Unity ..."），没有 Controllers / SubFeatures / partial 拆分，**不推荐作为主例**。

## 关键架构事实（必读）

### 1. BattleSessionFeature 是 40+ 文件的 partial class 巨型聚合根

主文件只声明 17 个字段（DI + State + Handles + 9 个 Controller），其余字段散落在各 partial 文件中（partial class 字段共享）。按职责分布在 6 个子目录：`Core/` `Gateway/` `Net/` `Sim/` `Snapshot/` `Editor/`。

### 2. Controllers 命名是 `Session*Controller`，不是 `BattleSession*Controller`

10 个 Controller 在 `Features/Controllers/`：`SessionOrchestrator` / `TickLoopController` / `SessionDispatchersController` / `SessionNetAdapterController` / `SessionReplayController` / `SessionPlanController` / `SessionEventsController` / `SessionSnapshotRoutingController` / `SessionWorldCatchUpController` 等。

Controller 是 Session 级共享的，**不冠 `Battle` 前缀**。

### 3. Host 接口反向桥接（旧 skill 完全未覆盖）

Controller 通过 `(state, handles, host)` 构造，`host` 是 Feature 本身（实现多个 `ISession*Host` 接口）。Feature 用**显式接口实现**把私有方法暴露给 Controller：

```csharp
// BattleSessionFeature.HostBridges.cs
void ITickLoopHost.TickRemoteDrivenLocalSim(float dt) => TickRemoteDrivenLocalSim(dt);
```

这是 Controller 与 Feature 解耦的关键机制。

### 4. Runtime 契约（旧 skill 完全未覆盖）

`BattleSessionFeature.Runtime.cs` 显式实现一组 `ISession*Runtime` 接口（`ISessionDispatchersRuntime / ISessionEventsRuntime / ISessionPlanRuntime / ISessionReplayRuntime / ISessionNetAdapterRuntime / ISessionTickLoopRuntime / ISessionLifecycleRuntime / ISessionGatewayRuntime / ISessionSnapshotRoutingRuntime`）。

SubFeature 通过 `FeatureModuleContext<BattleSessionFeature>` + `BattleSessionFeatureRuntimeAccess.TryGet<...>` 反向取这些契约。

### 5. WorldSystemBase 是另一层（ECS），与 Session 层并存

`Unity/Packages/com.abilitykit.world.entitas/Runtime/World/Base/WorldSystemBase.cs` 是 Entitas 系统基类，与 Session 层的 State/Handles/Controllers **两层并存、各管一层**，不互斥。

- `OnCleanup()`：每帧清理（基本不用，`ReactiveWorldSystemBase` 注释明说"空实现，资源释放在 TearDown 中进行"）
- `OnTearDown()`：销毁阶段释放（**始终执行，不受 Enabled 影响**）— 反注册放这里

## Sections

- [when_to_use.md](when_to_use.md) — 何时启用本 skill
- [required_context.md](required_context.md) — 重构前需确认的边界、字段归属、调用链
- [invariants.md](invariants.md) — 必须保持的不变量（State 纯数据、Handles partial、单向控制、注释规范、WorldSystemBase.OnTearDown）
- [key_files.md](key_files.md) — 当前真实关键文件清单（含 partial 文件命名模式）
- [output_expectations.md](output_expectations.md) — 重构完成后应看到的结果
- [procedure.md](procedure.md) — 推荐步骤（识别 → 收口 → 小批次 → 注释 → 验证 → 回归）
- [examples_and_troubleshooting.md](examples_and_troubleshooting.md) — 常见拆分点与典型问题

## 相关 skill

- 完整技能/触发/BUFF 模块速查见 [ability-kit](../ability-kit/SKILL.md)。
- 客户端预测/回滚见 [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)。
