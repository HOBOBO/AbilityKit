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

## skill 配置与排查流程
Action 配置
shoot_projectile 参数：launcherId + projectileId（从表读取）
调用链
TriggerAction -> MobaProjectileService.Launch -> ScheduleEmit -> MobaProjectileSyncSystem -> ActorEntity
表现层链路
snapshot -> BattleViewFeature 订阅 -> 播 OnSpawn/OnHit/OnExpire VFX
VFX entity 生命周期：DurationMs 到期清理

## 表现层排查：Projectile/VFX/Snapshot 事件链路

### 1) Projectile 表现链路（实体 -> 视图）
- 逻辑侧：MobaProjectileService 发射 / ScheduleEmit
- 同步侧：MobaProjectileSyncSystem 创建 bullet ActorEntity（逻辑实体）
- Game 表现侧：
  - ActorSpawnSnapshot -> BattleActorSpawnApplier -> CreateProjectile(netId, ownerNetId, entityCode)
  - ActorTransformSnapshot -> 更新 BattleTransformComponent -> DirtyEntities
  - BattleViewFeature.RefreshDirtyViews -> binder.Sync -> 创建/移动 GameObject（或 VFX）

### 2) Projectile 事件链路（用于播放 OnSpawn/OnHit/OnExpire VFX & SFX）
- 逻辑侧：IProjectileService 产出 Spawn/Hit/Exit events
- Snapshot：ProjectileEventSnapshot(4006)
- 表现侧：BattleViewFeature 订阅 ProjectileEventSnapshot
  - evt.TemplateId -> _configs.GetProjectile(templateId)
  - Spawn: proj.OnSpawnVfxId
  - Hit: proj.OnHitVfxId
  - Exit: proj.OnExpireVfxId
  - 位置：evt.X/Y/Z
  - 可跟随：若 evt.ProjectileActorId 可 resolve 为表现层实体，则 followId = entity.Id

### 3) VFX 生命周期排查
- VfxDTO.DurationMs > 0：到期自动移除
- BattleVfxManager.Tick(world) 每帧处理：
  - follow 同步位置
  - expire 到期销毁（Destroy GameObject + Destroy EC.Entity）

### 4) 常见问题检查点
- 没有播特效：
  - 检查 ProjectileEventSnapshot 是否有发出（4006）
  - 检查 evt.TemplateId 是否能在 Projectile 表查到配置
  - 检查 proj.OnSpawn/OnHit/OnExpireVfxId 是否配置 > 0
  - 检查 vfx.json 是否包含对应 vfxId，Resource 路径是否可 Resources.Load
- VFX 不自动消失：
  - 检查 VfxDTO.DurationMs 是否 > 0
  - 检查 BattleViewFeature.Tick 是否调用 _vfx.Tick(world)


在战斗中实现全链路溯源：无论事件由主动技能、buff、生效、被动等触发，且中间连锁触发任意多层，任意行为（Action）都能从 TriggerContext 的 中拿到最初来源（root cause）的信息，用于统一计算/日志/统计/规则判断。


Skill 事件强类型 payload
skill 事件 payload 统一使用 SkillCastContext
SkillCastContext 必须包含：
SkillId / SkillSlot / SkillLevel
CasterActorId / TargetActorId
AimPos / AimDir
SourceContextId（long，作为 origin.contextId 的唯一来源）
技能开始时创建 contextId（唯一一次）
在技能施放入口（例如 SkillExecutor.CastSkill）创建 SourceContextId：
ctx.SourceContextId = EffectSourceRegistry.CreateRoot(EffectSourceKind.SkillCast, skillId, caster, target, frame, originSource, originTarget)
这个 SourceContextId 在同一次施放生命周期内必须保持一致，不要在后续 phase 再 CreateRoot 导致 “skill 和 effect 不同 id”。
Skill 管线共享数据（pipeline shared data）
把 ctx.SourceContextId 写入 MobaSkillPipelineSharedKeys.SourceContextId
管线内任何 phase 若需要生成 effect/damage 等事件，应优先从 sharedData 读取该 id。
effect.execute / effect.apply 的中继链路规则
MobaEffectExecutionService 发布 effect.execute 类事件时，必须携带：
EffectTriggering.Args.Source/Target
EffectSourceKeys.SourceContextId
origin.*（至少 origin.contextId）
后续如果存在 “execute -> apply” 的转发订阅者：
必须使用 PublishInherited，不得覆盖 origin。
被动技能 direct-execute（无 EventId 的 trigger）规则
若是“直接 RunOnce 触发器”且需要合成 args：
需要写 source/target
若存在 SourceContextId：调用 EffectOriginArgsHelper.FillFromRegistry(...)
并确保 origin.source/target 至少存在（只在缺失时补）