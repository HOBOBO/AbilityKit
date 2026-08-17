# MOBA 标准技能流程指南

本文档定义 `moba.runtime` 当前推荐的主动技能黄金链路。它不是描述某一个固定技能 ID，而是描述一类表驱动主动技能的标准执行路径：输入或宿主发起施法，读取技能配置，构建 Pipeline，按时间轴触发 Effect，再由 Trigger Plan Action 进入投射物、伤害、Buff、召唤和表现事件。

后续新增主动技能时，优先沿这条链路扩展。只有当技能需要新的能力类型时，才新增 Phase、PlanAction 或服务。

## 黄金链路总览

```text
Input / Host Command
    -> SkillExecutor.CastBySlot / CastSkill
    -> IMobaSkillPipelineLibrary.TryGet
    -> TableDrivenMobaSkillPipelineLibrary
        -> SkillFlowMO
        -> SkillFlowChecksPhase
        -> SkillTimelinePhase
    -> SkillPipelineRunner.Start
        -> PreCast Pipeline
        -> Cast Pipeline
    -> MobaSkillPipelineStepSystem
        -> SkillExecutor.Step
        -> SkillPipelineRunner.Step
    -> SkillTimelinePhase.OnUpdate
        -> MobaEffectInvokerService.Execute(effectId, context)
        -> MobaEffectExecutionService.Execute
        -> TriggerPlan ActionRegistry
        -> PlanActionModule
            -> shoot_projectile / give_damage / add_buff / spawn_summon / play_presentation
    -> Event / Snapshot / View Runtime
```

这条链路覆盖 AbilityKit 的核心组合方式：

1. World DI 提供服务和配置。
2. Pipeline 管理技能阶段和生命周期。
3. Trigger Plan 管理效果动作编排。
4. Combat 模块执行投射物、伤害、Buff、召唤等玩法结果。
5. Event/Snapshot 把逻辑结果交给表现层、同步层和调试层。

## 推荐样例类型

推荐把“带时间轴的主动技能”作为复杂技能的标准样例，例如：

1. 施法者按槽位释放技能。
2. 技能读取 `SkillMO` 的 `PreCastFlowId` 和 `CastFlowId`。
3. PreCast 做冷却、目标、状态等检查。
4. Cast 的 Timeline 在指定时间点触发一个或多个 Effect。
5. Effect 对应 Trigger Plan。
6. Trigger Plan 执行 `shoot_projectile`、`give_damage`、`play_presentation` 等 PlanAction。
7. 投射物命中或伤害结果继续产生事件、快照和表现请求。

它比“立即伤害”更适合作为黄金链路，因为它同时覆盖输入、配置、管线、时间轴、效果、触发器、动作、Combat、表现事件和快照。

## 发起施法

主动技能默认从 `SkillExecutor` 进入。

常用入口：

1. `CastBySlot(actorId, slot)`：根据角色技能槽查找技能 ID。
2. `CastBySlot(actorId, slot, aimPos, aimDir, out failReason)`：带瞄准信息释放。
3. `CastSkill(actorId, skillId, slot, out failReason)`：直接按技能 ID 释放。
4. `HandleInput(actorId, SkillInputEvent)`：由输入事件转换为施法调用。

`SkillExecutor.CastSkillInternal` 的关键职责：

1. 校验 caster 和 skillId。
2. 从 actor transform 推导默认 aimPos / aimDir。
3. 调用 `IMobaSkillPipelineLibrary.TryGet` 获取 PreCast 与 Cast 配置。
4. 构造 `SkillCastRequest`。
5. 构造 `SkillCastContext`，写入技能等级和 cast sequence。
6. 创建技能溯源根节点。
7. 调用 `SkillPipelineRunner.Start`。

新增入口时，应该最终收敛到 `SkillExecutor`，不要绕过它直接创建 `SkillPipelineRunner`。

## 配置到 Pipeline

当前推荐的技能 Pipeline 来源是 `TableDrivenMobaSkillPipelineLibrary`。

它通过 `MobaConfigDatabase` 读取技能配置：

```text
SkillMO
    -> PreCastFlowId
    -> CastFlowId
    -> SkillFlowMO
    -> SkillFlowPhase
```

`TableDrivenMobaSkillPipelineLibrary.TryGet` 会：

1. 通过 skillId 查找 `SkillMO`。
2. 如果存在 `PreCastFlowId`，构建 PreCast Pipeline。
3. 要求 `CastFlowId` 有效，并构建 Cast Pipeline。
4. 把 `SkillFlowMO.Phases` 转换为 Pipeline Phase。

当前标准 Phase：

| 配置 Phase | 运行时 Phase | 职责 |
| --- | --- | --- |
| Checks | `SkillFlowChecksPhase` | 执行条件检查和 caster Required/Blocked 标签门控，失败时中止 Pipeline |
| Timeline | `SkillTimelinePhase` | 根据时间点触发 Effect |

新增技能时，优先组合 Checks 与 Timeline。新增 Phase 类型前，应先确认这个能力无法用条件、时间轴或 PlanAction 表达。

## PreCast 与 Cast

`SkillPipelineRunner` 把一次释放拆成两个可选阶段：

1. PreCast：前摇、冷却检查、施法状态检查、资源检查等。
2. Cast：真正执行技能效果、时间轴、投射物和伤害。

规则：

1. 没有 PreCast 配置时，直接进入 Cast。
2. PreCast 完成后自动链到 Cast。
3. 任一阶段失败时发布对应失败事件。
4. 中断或取消时写入终止快照。
5. Cast 完成时写入结束快照并结束溯源链路。

`SkillPipelineRunner` 会发布技能生命周期事件：

1. `skill.precast.start`
2. `skill.precast.complete`
3. `skill.precast.fail`
4. `skill.precast.interrupt`
5. `skill.cast.start`
6. `skill.cast.complete`
7. `skill.cast.fail`
8. `skill.cast.interrupt`

这些事件可以被 Triggering、日志、表现或调试系统订阅。新功能不要用旁路事件替代这些生命周期事件。

## 帧推进

技能 Pipeline 的帧推进由 `MobaSkillPipelineStepSystem` 负责。

它运行在：

```text
WorldSystemPhase.Execute
MobaSystemOrder.SkillPipelines
```

执行路径：

```text
MobaSkillPipelineStepSystem.OnExecute
    -> foreach ActorEntity with ActorId
    -> SkillExecutor.Step(actorId)
    -> SkillPipelineRunner.Step(deltaTime)
```

这意味着：

1. 技能时间轴与 World Clock 的 DeltaTime 对齐。
2. 技能推进属于帧内核心玩法执行阶段。
3. 依赖技能结果的系统应排在 `MobaSystemOrder.SkillPipelines` 之后。
4. 需要在技能前取消的系统可以排在 `MobaSystemOrder.SkillPipelines - 1` 附近。
5. 技能运行状态同步和清理应继续使用 `MobaSkillCastInstanceSyncSystem` 与 `MobaSkillCastDestroyCleanupSystem`。

## Timeline 到 Effect

`SkillTimelinePhase` 是表驱动技能的关键运行时 Phase。

它会：

1. 在 `OnEnter` 重置 `_nextIndex`。
2. 在 `OnUpdate` 根据 `context.ElapsedTime` 计算 elapsedMs。
3. 遍历时间点小于等于 elapsedMs 的 `SkillTimelineEventDTO`。
4. 调用 `MobaEffectInvokerService.Execute(e.EffectId, context)`。
5. 所有事件执行完或持续时间结束时 Complete。

时间轴事件只负责“在什么时间触发哪个 Effect”。它不应该直接写投射物、伤害或表现逻辑。

## Effect 到 Trigger Plan

`MobaEffectExecutionService.Execute(effectId, context)` 是 Effect 进入 Trigger Plan 的标准入口。

执行步骤：

1. 包装 `IAbilityPipelineContext` 为 Effect Context。
2. 提取 source/target 溯源信息。
3. 从 Trigger Plan 数据库查找 effectId 对应计划。
4. 创建 Effect 溯源根节点。
5. 为 Plan 中的 Action 创建子节点。
6. 调用 `TryExecutePlanByTriggerId(effectId, wrappedContext)`。
7. 结束当前溯源链路。

Effect 本身应当是“触发计划 ID + 上下文”的桥，而不是承载大量业务分支。复杂行为优先拆成多个 PlanAction。

## PlanAction

PlanAction 是技能效果落地到玩法结果的推荐扩展点。

当前代表动作：

| Action | Module | 结果 |
| --- | --- | --- |
| `shoot_projectile` | `ShootProjectilePlanActionModule` | 读取 launcher/projectile 配置并调用 `MobaProjectileService.Launch` |
| `give_damage` | `GiveDamagePlanActionModule` | 构造 `AttackInfo` 并进入 `DamagePipelineService.Execute` |
| `take_damage` | `TakeDamagePlanActionModule` | 根据上下文承受伤害 |
| `add_buff` | `AddBuffPlanActionModule` | 添加 Buff |
| `spawn_summon` | `SpawnSummonPlanActionModule` | 召唤实体 |
| `play_presentation` | `PlayPresentationPlanActionModule` | 发布表现请求事件 |

`PlanActionModuleRegistry` 会扫描带 `PlanActionModuleAttribute` 的模块并按 order 排序注册。

新增 PlanAction 时需要：

1. 定义 Args 类型。
2. 定义 Schema，把命名参数解析为 Args。
3. 新增 `PlanActionModule`。
4. 使用 `PlanContextValueResolver` 从 payload 读取 caster、target、aim、sourceContext 等上下文。
5. 通过 World DI 解析服务，不直接创建服务。
6. 给动作分配稳定 action id，并更新配置和文档。

## 投射物链路

`shoot_projectile` 是黄金链路里最推荐展示的复杂动作。

执行路径：

```text
SkillTimelinePhase
    -> MobaEffectExecutionService
    -> TriggerPlan
    -> ShootProjectilePlanActionModule
    -> MobaProjectileService.Launch
    -> Projectile Systems
    -> Projectile hit / tick / exit events
    -> Effect / Damage / Snapshot / Presentation
```

它适合作为样例，因为投射物会自然连接：

1. 配置表。
2. 施法上下文。
3. 空间位置与方向。
4. Combat Projectile 模块。
5. 命中事件。
6. 后续伤害或表现。

新增弹道类技能时，优先新增配置数据和 Trigger Plan，不要新增专用 System。

## 伤害链路

`give_damage` 展示了效果如何进入战斗计算。

执行路径：

```text
TriggerPlan
    -> GiveDamagePlanActionModule
    -> AttackInfo
    -> DamagePipelineService.Execute
    -> Damage pipeline events
    -> Snapshot / View Event
```

推荐做法：

1. PlanAction 只负责从上下文和 Args 组装 `AttackInfo`。
2. 伤害公式、减免、护盾、暴击等放在 Damage Pipeline。
3. 伤害结果通过事件和快照向外传播。
4. 技能逻辑不直接扣目标血量。

## 表现链路

`play_presentation` 是逻辑层向表现层发请求的标准方式。

它会发布：

1. `presentation.play`
2. `presentation.stop`

payload 使用 `PresentationEventArgs`，包含 templateId、目标、位置、持续时间、缩放、半径等表现参数。

逻辑层只发布表现请求，不创建 Unity 特效对象。Unity View Runtime 应订阅这些事件并完成实际表现。

## 快照与同步

技能运行状态由 Runner 和相关系统写入快照/同步实体。

关键点：

1. `SkillExecutor.FillRunningSnapshots` 暴露运行中技能快照。
2. `SkillExecutor.FillEndedSnapshots` 暴露结束技能快照。
3. `MobaSkillCastInstanceSyncSystem` 把运行和结束状态同步到 Entitas 实体。
4. `SkillPipelineRunner` 在完成、失败、取消时写入终态快照。
5. 影响战斗状态的 Action 应继续进入已有 Snapshot/Event 路径。

新增技能状态字段时，必须确认它是否需要回放、回滚、状态同步和表现重建。

## 新增主动技能清单

新增一个普通主动技能时，推荐按顺序做：

1. 在配置中定义 Skill，设置 `CastFlowId`，需要前摇时设置 `PreCastFlowId`。
2. 定义 SkillFlow：先 Checks，再 Timeline。
3. 在 Timeline 事件里配置 effectId 和触发时间。
4. 为 effectId 配置 Trigger Plan。
5. Trigger Plan 组合已有 PlanAction。
6. 如果需要新动作，新增 Args、Schema、PlanActionModule。
7. 如果需要新条件，新增 SkillCondition 并注册到 `SkillConditionRegistry`。
8. 如果需要新表现，新增表现配置，并通过 `play_presentation` 发布请求。
9. 如果影响状态同步，补充 Snapshot/Event 路径。
10. 更新本文档或具体技能文档。

## 不推荐路径

1. 不在 `SkillTimelinePhase` 里直接写技能特例。
2. 不在 `SkillExecutor` 里按 skillId 写分支。
3. 不在 PlanAction 内直接操作 Unity View。
4. 不绕过 `DamagePipelineService` 直接扣血。
5. 不绕过 `MobaProjectileService` 手写投射物实体。
6. 不把临时兼容 Action 当作新技能默认依赖。
7. 不为了一个技能新增全局单例或静态状态。

## 当前需要继续治理的点

1. 部分旧注释存在编码异常，后续应在清理 Legacy/Compat 时修正。
2. `DefaultMobaSkillPipelineLibrary` 更像备用或兼容实现，新功能默认使用 `TableDrivenMobaSkillPipelineLibrary`。
3. `SkillHandlerDTO` 和 Handler 系列存在旧流程痕迹，新增技能优先使用表驱动 SkillFlow + PlanAction。
4. `EffectExecuteMode.PublishEventOnly` 和 `InternalThenPublishEvent` 已被标记为 legacy publish removed，新配置不应依赖这类模式。
5. Stub Action 注册仅用于兼容初始化时序，不应作为正式扩展方式。

## 与其他文档的关系

1. `RuntimeArchitectureGuide.md`：说明技能链路应放在哪些目录和层次。
2. `StartupChainGuide.md`：说明技能相关服务和系统如何被启动链路装配。
3. `BootstrapFlowGuide.md`：说明 `TargetingAndSkills`、`PlanTriggering` 等 Stage 的扩展方式。
4. `SystemOrderGuide.md`：说明技能系统与其他系统的帧内顺序。
5. `EventGuide.md`：说明技能事件、表现事件和战斗事件如何传播。
6. `SnapshotGuide.md`：说明技能运行态与结果如何进入快照。
