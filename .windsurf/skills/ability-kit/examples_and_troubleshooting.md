---
name: ability-kit
section: examples
---

# Examples / Troubleshooting

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

- Action 配置：shoot_projectile 参数：launcherId + projectileId（从表读取）
- 调用链：TriggerAction -> MobaProjectileService.Launch -> ScheduleEmit -> MobaProjectileSyncSystem -> ActorEntity
- 表现层链路：snapshot -> BattleViewFeature 订阅 -> 播 OnSpawn/OnHit/OnExpire VFX
- VFX entity 生命周期：DurationMs 到期清理

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
