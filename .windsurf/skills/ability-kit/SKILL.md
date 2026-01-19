---
name: ability-kit
description: AbilityKit（MOBA）技能/触发器/被动/BUFF 模块速查与实现约束，用于快速定位代码入口、数据流与性能/池化约束。
---

# When to use

在你需要做以下事情时使用本 skill：

- **定位技能执行入口与事件派发**：找 `skill.*` 事件在哪里 Publish、为什么被动没触发。
- **新增/修改触发器行为**：例如新增 action/condition，或调整 trigger args/key 约定。
- **排查被动技能监听**：AllowExternal、外部事件过滤、订阅/反注册生命周期。
- **排查 BUFF 与效果联动**：buff.apply/buff.remove 事件、sourceContextId 溯源、Tick/Remove。

# Required context from the user

为了高效定位问题/实现功能，你最好提供：

- **事件名**：例如 `skill.cast.complete`
- **触发器 triggerId / 配置来源**：ability_triggers.json / AbilityModuleSO
- **触发对象**：caster/target actorId（或对应实体）
- **期望时序**：同帧生效 vs 下一帧生效

# Output expectations

本 skill 的输出应包含：

- **涉及的关键文件与入口点**（给出相对路径）
- **数据流/调用链说明**（Publish -> Subscribe -> Handler -> TriggerRunner.RunOnce）
- **高频路径性能约束**（避免 GC、优先池化/struct）
- **对工程既有约束的遵守**（Cleanup/TearDown、EventBus 一致性等）

# Project invariants / constraints (must follow)

- **性能优先**：默认战斗逻辑在高频路径，禁止不必要分配/反射/LINQ。
- **默认池化**：args/defs 使用 `PooledTriggerArgs` / `PooledDefArgs`。
- **生命周期**：不要在 Entitas `Cleanup()` 做一次性反注册，使用 `TearDown()`。
- **EventBus 一致性**：Subscribe 与 Publish 必须使用同一 `IEventBus` 实例（DI singleton）。
- **新写业务代码需中文注释**：说明目的、关键约束、边界条件。

# Key files (reference paths)

技能/事件：

- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Services/Skill/SkillExecutor.cs`
- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Services/Skill/SkillPipelineRunner.cs`
- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Services/Skill/SkillPipelineContext.cs`（含 `SkillCastRequest`）
- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Services/Skill/MobaSkillTriggering.cs`
- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Services/Skill/MobaSkillTriggerArgs.cs`

触发器：

- `Unity/Assets/Scripts/Ability/Share/Triggering/EventBus.cs`
- `Unity/Assets/Scripts/Ability/Share/Triggering/TriggerRunner.cs`
- `Unity/Assets/Scripts/Ability/Share/Triggering/Runtime/TriggeringWorldModule.cs`
- `Unity/Assets/Scripts/Ability/Share/Triggering/Runtime/WorldTriggerContextFactory.cs`

被动技能：

- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Systems/MobaPassiveSkillTriggerRegisterSystem.cs`
- `Unity/Assets/Scripts/Ability/Share/Base/TriggerDef.cs`（含 `AllowExternal`）

BUFF：

- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Systems/Buffs/MobaBuffApplySystem.cs`
- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Systems/Buffs/MobaBuffTickSystem.cs`
- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Systems/Buffs/MobaBuffRemoveSystem.cs`
- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Services/Buffs/MobaBuffService.cs`
- `Unity/Assets/Scripts/Ability/Share/Impl/Moba/Services/Buffs/MobaBuffTriggering.cs`

# Procedure (how to work on a skill-related task)

1. **确认事件源**
   - 找到事件派发处（通常是 `MobaSkillTriggering.Publish` 或具体 system/service）。
   - 确认 `IEventBus` 实例来自 DI（避免 publish/subscribe 不一致）。

2. **确认 payload 与 args 约定**
   - `TriggerEvent.Payload` 是否为 `SkillCastRequest` / `SkillPipelineContext`？
   - `evt.Args` 是否携带 `caster.actorId` / `effect.sourceActorId`？（影响被动外部过滤）

3. **确认订阅是否建立且生命周期正确**
   - 被动：`MobaPassiveSkillTriggerRegisterSystem` 是否为对应实体注册了 listener？
   - 反注册是否只发生在 entity destroy / system TearDown？

4. **确认 trigger 执行路径**
   - `TriggerRunner.RunOnce` 是否被调用？
   - 对外部事件：`AllowExternal=false` 的 entry 应被过滤。

5. **性能与池化检查**
   - 高频路径是否引入 `new List/Dictionary`、LINQ、闭包？
   - args 是否能复用并避免复制（临时注入/恢复优先）。

# Examples

## Example A: skill.cast.complete 触发被动

检查点：

- Publish：`SkillPipelineRunner.Step` 在 Cast Completed 派发 `skill.cast.complete`
- Args：`MobaSkillTriggering.Args.CasterActorId` 必须存在
- 被动过滤：外部 caster 事件仅 `AllowExternal=true` 的 trigger entry 执行

## Example B: 执行 effect_execute

- Action：`effect_execute`
- Payload：必须是 `SkillPipelineContext` 或 `SkillCastRequest`
- Service：通过 DI 解析 `MobaEffectExecutionService` 并执行 effect

## Buff 不触发/触发异常排查
- 先确认 BuffDTO 字段：
  - OnAddEffects / OnRemoveEffects / OnIntervalEffects / IntervalMs
- Apply 阶段：
  - 文件：MobaBuffApplySystem / MobaBuffService.ApplyBuffImmediate
  - 事件：buff.apply、buff.apply.<effectId>
- Remove 阶段：
  - 文件：MobaBuffRemoveSystem / MobaBuffTickSystem（过期）/ MobaBuffService.RemoveBuffImmediate
  - 事件：buff.remove、buff.remove.<effectId>
- Interval 阶段：
  - 文件：MobaBuffTickSystem（IntervalRemainingSeconds 计时）
  - 事件：buff.interval、buff.interval.<effectId>
- 如果被动没触发：
  - 检查 args 是否包含 effect.sourceActorId（否则 AllowExternal 外部过滤无法判断来源）
