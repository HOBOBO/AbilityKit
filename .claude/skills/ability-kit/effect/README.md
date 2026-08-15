# Effect 系统（替代旧 EffectSource）

> **重要**：旧 skill 中的 `EffectSourceRegistry` / `EffectSourceSnapshot` / `EffectSourceLiveRegistry` / `EffectSourceKeys` / `EffectSourceDebuggerWindow` / `ContextId` / `RootId` / `ParentId` **已全部从代码中删除**。`com.abilitykit.demo.moba.runtime/Runtime/Domain/Ability/Impl/Moba/EffectSourceCompat.cs` 仅留空命名空间占位用于避免旧引用编译报错。

## 替代方案：新 Effect 系统

位置：`Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/`（命名空间 `AbilityKit.Ability.Share.Effect`，部分类型在 `AbilityKit.Effect`）。

## 核心类清单

| 类 | 文件 | 作用 |
|----|------|------|
| `EffectService` | `EffectService.cs` | 核心服务，持有 `IEventBus`（第一套）+ `TriggerRunner`（第一套）；`PublishEffectApply/Tick/Remove`，`Publish`，`EvaluateOnce/RunOnce` |
| `EffectInstance` | `EffectInstance.cs` | 运行实例：`Id` / `Spec` / `ElapsedSeconds` / `RemainingSeconds` / `NextTickInSeconds` / `StackCount` / `State` dict。**计时字段已定点化（2026-08）**：内部 `ElapsedRaw` / `RemainingRaw` / `NextTickRaw`（Q32.32 raw long 累加，internal），float 属性为边界视图——触发事件/表现层继续读 float，零改动；`EffectContainer.Step` 全整数运算 |
| `GameplayEffectSpec` | `GameplayEffectSpec.cs` | 配置态 |
| `EffectContainer` | `EffectContainer.cs` | 容器（持有效果实例列表） |
| `EffectExecutionContext` | `EffectExecutionContext.cs`（ns `AbilityKit.Effect`） | 执行上下文，含 `Source` / `Target` |
| `EffectDurationPolicy` | `EffectDurationPolicy.cs` | 持续策略枚举 |
| `IEffectComponent` / `AttributeEffectComponent` / `TriggerEventEffectComponent` | `Components/` | 效果组件 |
| `IEffectEventSink` / `EventBusEffectEventSink` | 同目录 | 事件下沉（默认实现走第一套 EventBus） |
| `EffectTriggering` | `EffectTriggering.cs` | 事件名/arg 键常量 |

## 事件名（EffectTriggering.Events）

- `Apply` — `"effect.apply"`
- `Tick` — `"effect.tick"`
- `Remove` — `"effect.remove"`

## Args 键（EffectTriggering.Args）

- `Source` / `Target` / `Spec` / `Instance` / `InstanceId` / `StackCount` / `ElapsedSeconds` / `RemainingSeconds`

## 溯源（替代旧 ContextId/RootId/ParentId）

旧 EffectSource 的"事件溯源树"机制已删除。当前溯源方式：

- 通过 `EffectExecutionContext.Source` / `Target` 在事件传播时携带
- 通过 `EffectTriggering.Args.SourceActorId` / `TargetActorId` 在订阅侧读取
- 不再有独立的 Registry / Snapshot / LiveRegistry / DebuggerWindow

## 关键文件入口

- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectService.cs`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectInstance.cs`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/GameplayEffectSpec.cs`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectContainer.cs`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectTriggering.cs`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EffectExecutionContext.cs`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/Components/` — `IEffectComponent` / `AttributeEffectComponent` / `TriggerEventEffectComponent`
- `Unity/Packages/com.abilitykit.ability/Runtime/Ability/Effect/EventBusEffectEventSink.cs`

## 注意

- Effect 系统使用**第一套** EventBus（`AbilityKit.Ability.Triggering.IEventBus`）。
- moba 业务事件（buff/damage/skill）走**第二套** EventBus（`AbilityKit.Triggering.Eventing.IEventBus`）。
- 跨总线订阅/发布仍然收不到事件，详见 [triggering_engines.md](triggering_engines.md)。
