# Examples / Troubleshooting

## A. skill.cast.complete 触发被动

检查点：

- Publish：`SkillPipelineRunner.Step` 在 Cast Complete 阶段调 `MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastComplete, ctx)`
- Args：`MobaSkillTriggerArgs` 常量（`caster.actorId` / `target.actorId` / `skill.id`）
- 被动注册：`MobaPassiveSkillTriggerRegisterSystem.OnEntityChanged` → `MobaPassiveSkillLifecycleService.SyncActorPassives`
- AllowExternal：在 Source/Plan DTO 层配置（不在 `TriggerDef`），详见 [triggering_engines.md](../triggering/two_engines.md)

## B. effect_execute（PlanAction）

旧 skill 提到的 `effect_execute` action 已演化为 PlanActionModule 体系：

- 入口：`MobaTriggerExecutionGateway` + `MobaTriggerPlanSubscriptionService`
- 动词清单（`com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/`）：
  - **Skill**：`AddBuff` / `GiveDamage` / `TakeDamage` / `ShootProjectile` / `SpawnArea` / `SpawnSummon` / `StartCooldown` / `ConsumeResource` / `AddShield` / `RemoveShield` / `RemoveBuff` / `RemoveArea` / `RemoveSummon` / `CancelSkill`
  - **Gameplay**：`SetGameplayVar` / `AddGameplayVar` / `EndGame`
  - **Presentation**：`PlayPresentation` / `Emit`
  - **Motion**：`Blink`
  - **Debug**：`DebugLog`
- 每个 PlanAction = Args + Schema + PlanActionModule，通过 `[AutoPlanAction]` 特性自动注册到 `ActionSchemaRegistry`

## C. BUFF 不触发/触发异常排查

- 先确认 BuffDTO 字段（`Configs/moba/buffs.json`）：
  - `OnAddEffects` / `OnRemoveEffects` / `OnIntervalEffects` / `IntervalMs`
- Apply 阶段：
  - 入口：`MobaBuffService.ApplyBuffImmediate` → `BuffLifecycleExecutor.Apply` → `BuffApplyFlow`
  - 事件：`MobaBuffTriggering.Events.Apply`（="buff.apply"）+ 派生 `buff.apply.<effectId>`
- Remove 阶段：
  - 入口：`MobaBuffService.RemoveBuffImmediate` → `BuffLifecycleExecutor.Remove/EndRuntime` → `BuffEndFlow`
  - 事件：`buff.remove` + `buff.remove.<effectId>`
- Interval 阶段：
  - 驱动：`MobaBuffLifecycleReconcileSystem` + `BuffContinuousIntervalHandler`
  - 事件：`buff.interval` + `buff.interval.<effectId>`
- 如果被动没触发：
  - 检查订阅的 EventBus 是否是第二套（`AbilityKit.Triggering.Eventing.IEventBus`）
  - 检查 `BuffEventArgs` 字段是否包含所需 actorId
  - 检查 `AllowExternal` 在 Plan DTO 上的配置

## D. Projectile / VFX 表现层排查

注意：moba demo 的表现层在 `com.abilitykit.demo.moba.view.runtime`，具体路径以当前代码为准。调用链骨架：

```
逻辑侧：IProjectileService 产出 Spawn/Hit/Exit events
  → 第二套 EventBus 派发
  → Snapshot：ProjectileEventSnapshot(4006)
  → 表现侧：BattleViewFeature 订阅
    → evt.TemplateId → _configs.GetProjectile(templateId)
    → Spawn: proj.OnSpawnVfxId / Hit: proj.OnHitVfxId / Exit: proj.OnExpireVfxId
```

排查点：

- 没有播特效：
  - 检查 `ProjectileEventSnapshot` 是否发出（4006）
  - 检查 `evt.TemplateId` 是否能在 Projectile 表查到配置
  - 检查 `proj.OnSpawn/OnHit/OnExpireVfxId` 是否配置 > 0
  - 检查 `vfx.json` 是否包含对应 vfxId
- VFX 不自动消失：检查 `VfxDTO.DurationMs > 0` 与 `BattleVfxManager.Tick(world)` 是否被调用

## E. 事件收不到的统一排查（**最常见问题**）

按优先级排查：

1. **EventBus 类型对了吗？**（90% 的问题）
   - moba 业务必须用第二套（`AbilityKit.Triggering.Eventing.IEventBus`）
   - ability Effect 层用第一套（`AbilityKit.Ability.Triggering.IEventBus`）
   - 详见 [triggering_engines.md](../triggering/two_engines.md)
2. **EventKey<TArgs> 的 TArgs 类型对了吗？**
   - 第二套按 `TArgs` 类型分 channel，订阅时类型必须与发布一致
3. **phase / priority 是否被屏蔽？**
   - 第二套按 phase 排序，若前序 phase `ExecutionControl.StopPropagation` 则后续不执行
4. **AllowExternal 配置对了吗？**
   - 字段在 `TriggerSourceConfig.AllowExternal` 或 Plan DTO 上
5. **订阅生命周期？**
   - 是否在 `WorldSystemBase.OnTearDown()` 反注册（不是 `OnCleanup`）

## F. 常见错误对照

| 症状 | 根因 | 排查 |
|------|------|------|
| 事件订阅永远不触发 | 跨 EventBus 订阅（旧/新混用） | grep `using` + `IEventBus` 命名空间 |
| TriggerDef 找不到 AllowExternal | 字段已下移到 Source/DTO 层 | 看 `TriggerSourceConfig.cs:161` |
| SkillExecutor 找不到 | 类已删除 | 改查 `SkillCastCoordinator` + `SkillPipelineRunner` |
| MobaBuffApplySystem 找不到 | 类已删除 | 改查 `MobaBuffService` + `BuffLifecycleExecutor` |
| AbilityPipelineLiveRegistry 找不到 | 改名 | 改查 `EditorPipelineRegistry` |
| PipelineGraphAsset 找不到 | 改为静态类 | 改查 `PipelineGraph`（`Runtime/Graph/PipelineGraph.cs`） |
| EffectSourceRegistry 找不到 | 完全删除 | 改查新 Effect 系统（`EffectService` 等） |
| EntitasContextsFactory 找不到 | 接口名带 I | 改查 `IEntitasContextsFactory`（moba 实现 `MobaEntitasContextsFactory`） |
| LogicWorldServer 找不到 | 仅是示例类 | 改查 `LogicWorldServerExample`（`host.extension/Example/`） |
