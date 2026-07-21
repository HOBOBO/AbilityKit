# 触发器引擎（Triggering）

> 本目录覆盖 AbilityKit 的两套触发器实现。理解两套差异是定位"事件收不到"类问题的前提。

## 速查表

| 维度 | 第一套（旧/字符串引擎） | 第二套（新/Plan 引擎） |
|------|------------------------|----------------------|
| **位置** | `com.abilitykit.ability/Runtime/Ability/Triggering/` | `com.abilitykit.triggering/Runtime/` |
| **命名空间** | `AbilityKit.Ability.Triggering.*` | `AbilityKit.Triggering.*`（`IEventBus` 在 `AbilityKit.Triggering.Eventing`） |
| **EventBus 键** | `string eventId` | 强类型 `EventKey<TArgs>` |
| **事件载荷** | `TriggerEvent(Id, payload, args:IReadOnlyDictionary<string,object>)` | 强类型 `TArgs`（`BuffEventArgs` / `SkillCastContext` / `AttackInfo` / `DamageResult`） |
| **派发模式** | 仅立即（同步） | Immediate 或 Queued（`Flush` 多轮，带死循环保护） |
| **TriggerRunner** | 非泛型 `TriggerRunner` | 泛型 `TriggerRunner<TCtx>` |
| **行为定义** | `TriggerDef` + `TriggerCompiler` → `TriggerInstance` | `TriggerPlan` 行为树 |
| **moba 是否使用** | **否**（仅 ability 包 Effect 层） | **是**（moba 全部业务事件） |

详细对比、错误案例、判断"我在用哪套"的方法 → [two_engines.md](two_engines.md)

## 关键文件

### 第一套（旧引擎，仅 ability 包 Effect 层）

- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Triggering/EventBus.cs` — string eventId
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Triggering/TriggerRunner.cs` — 非泛型
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Triggering/Interface/IEventBus.cs`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Triggering/PooledTriggerArgs.cs` / `PooledDefArgs.cs`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Triggering/Runtime/TriggeringWorldModule.cs` — 世界模块（moba 不再装配）
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Triggering/Json/AbilityTriggerJsonDatabase.cs` — 弱类型 JSON
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Base/TriggerDef.cs` — 纯 `(EventId, Conditions, Actions)`，**已无 AllowExternal 字段**

### 第二套（新引擎，moba 生产用）

- `Unity/Packages/com.abilitykit.triggering/Runtime/Events/EventBus.cs` — 泛型 `EventKey<TArgs>`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Events/IEventBus.cs`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Events/EventBusOptions.cs` — Immediate / Queued
- `Unity/Packages/com.abilitykit.triggering/Runtime/Triggering/Runner/TriggerRunner.cs` — 泛型 `TriggerRunner<TCtx>`，phase+priority 排序
- `Unity/Packages/com.abilitykit.triggering/Runtime/Triggering/Contracts/ITrigger.cs` — `ITrigger<TArgs, TCtx>`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Triggering/Contracts/ITriggerCue.cs` / `ITriggerLifecycle.cs` / `ITriggerObserver.cs`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Plans/Model/TriggerPlan.cs` — 行为树模型
- `Unity/Packages/com.abilitykit.triggering/Runtime/Plans/Builders/TriggerPlanDsl.cs` / `TriggerPlanFactory.cs`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Plans/Executables/` — Sequence / Selector / Parallel / Random / Repeat / Until / If / Invert / Succeed / Fail / Scheduled / ActionCall / Metadata
- `Unity/Packages/com.abilitykit.triggering/Runtime/Plans/Execution/PlannedTriggerActionExecutor.cs` / `ActionSchemaRegistry.cs`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Plans/Validation/TriggerPlanDatabase.cs` / `CycleDetectorValidator.cs` / `UgcLimitsValidator.cs`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Plans/Serialization/Json/TriggerPlanJsonDatabase.cs` — Plan JSON 加载（含 AllowExternal 字段 line 84）

### AllowExternal 当前位置

旧 `TriggerDef.AllowExternal` **已删除**。当前在配置/DTO 层：

- `Unity/Packages/com.abilitykit.ability/Runtime/Config/Source/TriggerSourceConfig.cs:161` — `public bool AllowExternal = false;`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Plans/Serialization/Json/Database/TriggerPlanJsonDatabase.cs:84`
- `Unity/Packages/com.abilitykit.triggering/Runtime/Executables/Conversion/ExecutableDto.cs:172`
- Editor 导出 DTO：`TriggerPlanExportDtos.cs:22` / `ReadableTriggerPlanDtos.cs:78`

## 第二套派发链

```
EventBus.Publish<TArgs>(EventKey<TArgs>, in TArgs)
  → EventChannel<TArgs>.DispatchImmediate 或 Enqueue（按 EventBusOptions）
  → TriggerRunner<TCtx>.Dispatcher<TArgs>.OnEvent → Dispatch
    → 按 (phase, priority, registrationOrder) 排序遍历 TriggerRunnerEntry
    → entry.Trigger.Evaluate(args, execCtx)  [条件]
    → entry.Trigger.Execute(args, execCtx)   [动作]
    → 全程 ITriggerLifecycle / ITriggerObserver / ITriggerTracer / ITriggerCue 回调
    → ActionSchedulerManager 推进持续动作
```

## 第二套世界装配

`com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/Stages/WorldModulesStage.cs:34` 注册：

- `AbilityKit.Triggering.Eventing.IEventBus`（Singleton）
- `TriggerRunner<IWorldResolver>`（Scoped）
- 各 Registry（FunctionRegistry / ActionRegistry / BlackboardSchemaRegistry 等）

## 第二套新增能力（旧 skill 未覆盖）

- **TriggerPlan 行为树**：13 种 Executable 节点
- **校验器**：CycleDetector / UgcLimits / ExecutableValidator
- **Cue / Lifecycle / Observer / Tracer**：可观测的派发链
- **ExecutionControl**：hard-stop / cancel / stop-propagation
- **phase + priority 排序**：精细化派发顺序
- **PayloadStruct**：强类型字段访问（编译期布局）
- **Numeric 表达式**：与第一套并行的实现
