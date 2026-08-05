# Project invariants / constraints

> 本节所有约定均基于 2026-08-04 实际源码核校，标注判定与证据。

## 1. 中文注释（部分有效 — 事实约定，无强制）

**约定**：注释使用中文；非公共、显而易见的代码可以不写注释。

**证据**：
- 无 `.editorconfig`、无 `CONTRIBUTING.md`、无 `CLAUDE.md`、无注释规范文档强制语言
- 抽查近期改动文件，凡写注释的几乎都是中文（`MobaBuffService.cs` 类级中文 summary、`TriggerRunner.cs` 中文内联、`WorldSystemBase.cs` XML doc 中文）
- **反例**：`EffectService.cs` / `EffectContainer.cs` / `AddBuffActionConfig.cs` 等类几乎无注释

**建议**：写中文注释，但不要为写而写；公共 API 与复杂逻辑必须有，简单 getter/setter 可不写。

## 2. 池化 args/defs（仍然有效 — 完整保留）

**约定**：高频路径 args/defs 使用 `PooledTriggerArgs` / `PooledDefArgs`，按 `Rent()` + `Dispose()` 模式使用。

**证据**：
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Triggering/PooledTriggerArgs.cs` — `sealed class : Dictionary<string,object>, IDisposable, IPoolable`，`static readonly ObjectPool<PooledTriggerArgs> _pool = Pools.GetPool(..., defaultCapacity: 64, maxSize: 2048)`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Triggering/PooledDefArgs.cs` — 同构，`defaultCapacity: 128, maxSize: 4096`
- 池基础设施：`com.abilitykit.core/Runtime/Pooling/Core/ObjectPool.cs` / `PoolManager.cs` / `Pools.GetPool(...)`
- 实际使用：`TriggerRunner._localVarsPool.Get()/Release`、`EffectService.PooledTriggerArgs.Rent() + finally Dispose()`、`EventBusEffectEventSink`、`AddBuffActionConfig`
- `TriggerRunner.Dispatch` 在分发结束后 `if (disposeArgs && evt.Args is IDisposable d) d.Dispose()`（自动归还）

**注意**：`PooledTriggerArgs/PooledDefArgs` 属于第一套触发器（ability 包）；moba 生产用的第二套触发器使用强类型 `TArgs`，不需要池化字典。两套不要混。

## 3. Cleanup vs TearDown（仍然有效 — 精确化为 WorldSystemBase）

**约定**：继承 `WorldSystemBase` 的 system，反注册放在 `OnTearDown()`，不要放在 `OnCleanup()`。

**证据**：
- `Unity/Packages/com.abilitykit.world.entitas/Runtime/World/Base/WorldSystemBase.cs` 实现 `Entitas.IInitializeSystem, IExecuteSystem, ICleanupSystem, ITearDownSystem`，明确两阶段：
  - `Cleanup()` 每帧调用（注释"每帧清理阶段调用"）
  - `TearDown()` 销毁阶段调用（注释"销毁阶段调用，始终执行，不受 Enabled 影响"）
- 子类重写 `protected virtual void OnTearDown()` / `OnCleanup()`
- 全仓库 4 处 `Cleanup()`、7 处 `TearDown()`，量级低且受控

## 4. EventBus 一致性（重大改写 — 存在两个不互通的 IEventBus）

**约定**：Subscribe 与 Publish 必须**同接口类型 + 同 DI 实例**。仓库内同时存在两个 `IEventBus`，互不兼容，订阅/发布不会互通。

**两套 IEventBus**：

| 类型 | 命名空间 | 文件 | DI 注册 |
|------|---------|------|---------|
| 旧/能力内 | `AbilityKit.Ability.Triggering.IEventBus` | `com.abilitykit.ability/Runtime/Ability/Triggering/Interface/IEventBus.cs` | `DefaultWorldServicesModule.cs:25` Singleton |
| 新/通用 | `AbilityKit.Triggering.Eventing.IEventBus` | `com.abilitykit.triggering/Runtime/Events/IEventBus.cs` | `WorldModulesStage.cs:34` Singleton |

**实际使用**（grep 验证）：
- 新总线（moba 生产）：`DamagePipelineService` / `MobaProjectileSyncSystem` / `MobaUnitDeathSubscriber` / `MobaSkillTriggering` / `BuffEventPublisher`
- 旧总线（ability Effect 层）：`EffectService` / `TriggerRunner`（第一套）/ `EffectContainer`

**陷阱**：moba 业务代码默认用新总线；如果误用旧总线订阅，永远不会收到 moba 事件，反之亦然。

## 5. 性能优先（部分有效 — 改为代码风格自律）

**约定**：Runtime/热路径手写 `for` 循环、预分配容量、`in` 传结构体、池化 args；LINQ / 反射 / `new List<>` 仅限 Editor / 序列化 / 构建期。允许必要分配（如 handler 快照），但需在注释里写明理由。

**证据**：
- `AbilityKit.Analyzer` 目前定义框架规则 `AK1001`（禁止命名空间）、`AK1002`（禁止程序集）、`AK1003`（约束包名不匹配）和 `AK1004`（配置表 DTO/MO factory 不成对）。`AK1001`-`AK1003` 仍由约束配置驱动，`AK1004` 负责在编译期拦截单侧 factory。
- MOBA 专用声明规则（`AKSG1001`-`AKSG9005`、`AK2001`-`AK2006` 等）位于 `com.abilitykit.demo.moba.codegen`，不应回流到框架 Analyzer。
- **仍没有 LINQ/分配/反射性能检测**；这些约束继续依靠代码风格、自测与 Code Review。
- 高频路径代码风格：`com.abilitykit.triggering/Runtime/Events/EventBus.cs` `Publish<TArgs>(..., in TArgs)` + `for (int i=0; i<_flushables.Count; i++)` + 预容量；`EffectContainer.cs` 手写循环、无 LINQ；`TriggerRunner.ExecuteInternal` 手写循环。
- LINQ 集中在 `Editor/CodeGen/TriggerCodegenMenu.cs` 与 `Runtime/Dsl/TriggerPlanDsl.cs:218`（构建期），Runtime 热路径无 LINQ。
- 必要分配的反例：`TriggerRunner.cs:235` `new List<IEventHandler>(handlers)` 注释"创建快照，避免分发过程中处理器列表被修改"。

**结论**：Analyzer 现在负责依赖边界、约束配置和配置 factory 配对；MOBA 声明合法性由 MOBA Analyzer 负责；性能仍靠自律 + Code Review。

## 6. asmdef 引用不传递（仍然有效 — 完整保留）

**约定**：asmdef `references` 字段不传递；引用 AbilityKit.Ability 不会自动得到 Combat.Damage / Combat.EntityManager / Combat.Targeting 等，必须在自身 asmdef `references` 里逐条声明。

**证据**：
- `com.abilitykit.demo.moba.runtime/Runtime/com.abilitykit.demo.moba.runtime.asmdef` 显式列 **34** 条 references，其中包含 AbilityKit.Ability 下游的 `Combat.Damage` / `Combat.EntityManager` / `Combat.Targeting` / `Combat.Collision.Abstractions` / `Combat.Projectile`（即使 AbilityKit.Ability 已引用它们）。
- `com.abilitykit.ability/Runtime/com.abilitykit.ability.asmdef` 列 12 条；`com.abilitykit.triggering/.../com.abilitykit.triggering.asmdef` 列 5 条（含 `AbilityKit.Analyzer`，少见用法）；`com.abilitykit.core` references 为空（最底层）。

详见 [upm_asmdef_notes.md](upm_asmdef_notes.md)。
