# 两套触发器引擎对比

> 项目当前**同时存在两套完全独立、互不依赖**的触发器实现。理解两者差异是定位"事件收不到"类问题的前提。

## 速查表

| 维度 | 第一套（旧/字符串引擎） | 第二套（新/Plan 引擎） |
|------|------------------------|----------------------|
| **位置** | `com.abilitykit.ability/Runtime/Ability/Triggering/` | `com.abilitykit.triggering/Runtime/` |
| **命名空间** | `AbilityKit.Ability.Triggering.*` | `AbilityKit.Triggering.*`（含 `AbilityKit.Triggering.Eventing.IEventBus`） |
| **EventBus 键** | `string eventId` | 强类型 `EventKey<TArgs>` |
| **事件载荷** | `TriggerEvent(Id, payload, args:IReadOnlyDictionary<string,object>)` | 强类型 `TArgs`（如 `BuffEventArgs` / `SkillCastContext` / `AttackInfo` / `DamageResult`） |
| **派发模式** | 仅立即（`Publish` 同步） | Immediate 或 Queued（`Flush` 多轮，带死循环保护） |
| **TriggerRunner** | 非泛型 `TriggerRunner` | 泛型 `TriggerRunner<TCtx>` |
| **触发器定义** | `TriggerDef` + `ConditionDef` + `ActionDef` | `ITrigger<TArgs, TCtx>` |
| **行为定义** | `TriggerCompiler` 编译 `TriggerDef` → `TriggerInstance` | `TriggerPlan` 行为树（Sequence/Selector/Parallel/If/Repeat/Until/Scheduled/ActionCall...） |
| **数据来源** | `AbilityTriggerJsonDatabase`（弱类型 JSON） | `TriggerPlanJsonDatabase`（Plan JSON，多形态 + 校验器） |
| **Blackboard / 变量** | 自有 `IBlackboard` / `BlackboardImpl` / Numeric 表达式 | 自有 `Blackboard`（Schema/Core/Resolvers/Mapping）+ `PayloadStruct` + Numeric 表达式 |
| **世界装配** | `TriggeringWorldModule`（注册 TriggerRunner/TriggerRegistry/ITriggerContextFactory） | `WorldModulesStage` 手动注册 `EventBus`(singleton) + `TriggerRunner<IWorldResolver>`(scoped) + Registry |
| **排序** | 注册顺序 | phase + priority + registrationOrder |
| **观测** | 无 | `ITriggerCue` / `ITriggerLifecycle` / `ITriggerObserver` / `ITriggerTracer` |
| **执行控制** | 无 | `ExecutionControl`（hard-stop / cancel / stop-propagation）、`EInterruptPolicy`、`ShortCircuitReason` |
| **moba 是否使用** | **否**（moba runtime 内零引用 TriggeringWorldModule / 第一套 EventBus） | **是**（技能/被动/BUFF/伤害/投射/区域全部经此） |
| **服务范围** | 仅 ability 包自己的 Effect 层（EffectService / EffectContainer / ProjectileTriggering / AreaTriggering） | moba demo 全部业务事件 |

## 判断"我在用哪套"

读代码的 `using` 与 `IEventBus` 来源：

- `using AbilityKit.Ability.Triggering;` → 第一套（旧）
- `using AbilityKit.Triggering.Eventing;` → 第二套（新）

DI 注入：

- 第一套：`DefaultWorldServicesModule.cs:25` 注册 `AbilityKit.Ability.Triggering.IEventBus`（Singleton）
- 第二套：`WorldModulesStage.cs:34` 注册 `AbilityKit.Triggering.Eventing.IEventBus`（Singleton）

## 何时用哪套

### 改 moba 业务（技能/被动/BUFF/伤害/投射/区域）

**必须用第二套**。示例（订阅 BUFF apply 事件）：

```csharp
// 注入第二套 IEventBus
public sealed class MySystem
{
    private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;

    public MySystem(AbilityKit.Triggering.Eventing.IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe(
            new EventKey<BuffEventArgs>(MobaBuffTriggering.Events.Apply),
            args => HandleBuffApply(args));
    }

    private void HandleBuffApply(BuffEventArgs args) { /* 强类型访问 args.BuffId / args.TargetActorId ... */ }
}
```

### 改 ability 包 Effect 层（EffectService / EffectContainer / ProjectileTriggering / AreaTriggering）

用第一套。示例：

```csharp
// 第一套：字符串键 + PooledTriggerArgs
var args = PooledTriggerArgs.Rent();
args["effect.sourceActorId"] = casterId;
args["effect.targetActorId"] = targetId;
_triggerRunner.Dispatch(new TriggerEvent("effect.apply", spec, args), disposeArgs: true);
// args 在 Dispatch 内自动 Dispose 归还池
```

## 常见错误

### 错误 1：跨总线订阅

```csharp
// 错：用第一套 EventBus 订阅 moba 业务事件（永远不会收到）
_ability_Kit.Ability.Triggering.IEventBus.Subscribe("buff.apply", handler);

// 对：moba 业务事件走第二套
triggering_Eventing.IEventBus.Subscribe(new EventKey<BuffEventArgs>("buff.apply"), handler);
```

### 错误 2：TriggerDef 上找 AllowExternal

旧文档说 `TriggerDef.AllowExternal`。**实际**：`TriggerDef`（`com.abilitykit.ability/Runtime/Ability/Base/TriggerDef.cs`）现在是纯 `(EventId, Conditions, Actions)` 元组，已无 AllowExternal 字段。

AllowExternal 当前位置（配置/DTO 层）：
- `com.abilitykit.ability/Runtime/Config/Source/TriggerSourceConfig.cs:161` — `public bool AllowExternal = false;`
- `com.abilitykit.triggering/Runtime/Plans/Serialization/Json/Database/TriggerPlanJsonDatabase.cs:84`
- `com.abilitykit.triggering/Runtime/Executables/Conversion/ExecutableDto.cs:172`
- Editor 导出 DTO：`TriggerPlanExportDtos.cs:22` / `ReadableTriggerPlanDtos.cs:78`

## 第二套的额外能力（旧 skill 完全未覆盖）

- **TriggerPlan 行为树**：`Sequence / Selector / Parallel / Random / Repeat / Until / If / Invert / Succeed / Fail / Scheduled / ActionCall / Metadata`
- **校验器**：`CycleDetectorValidator` / `UgcLimitsValidator` / `TriggerPlanExecutableValidator`
- **Cue / Lifecycle / Observer / Tracer**：可观测的派发链
- **ExecutionControl**：hard-stop、cancel、stop-propagation
- **phase + priority 排序**：精细化派发顺序控制
- **PayloadStruct**：强类型字段访问（编译期布局）
- **Numeric 表达式**：与第一套并行的实现
