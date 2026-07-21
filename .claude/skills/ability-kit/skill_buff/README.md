# 技能施放与 BUFF 业务

> moba demo 的技能施放链路与 BUFF 生命周期。**全部基于第二套触发器**（`AbilityKit.Triggering.Eventing.IEventBus`）。

## 技能施放调用链

```
SkillCastCoordinator.TryCastSkill / CastSkill
  → CastSkillInternal
    → _preparation.Prepare(SkillCastPreparationInput)
    → StartPreparedCast
      → _runnerRegistry.GetOrCreate(actorId)
      → runner.Start(preCastConfig, preCastPhases, castConfig, castPhases,
                     abilityInstance, in req, ctx, out failReason, in policy)
        → StartPreCast → (完成则) StartCast
          → new SkillPipelineContext().Initialize(instance, req, triggerContext)
          → pipeline.Start(castConfig, context) → run.Tick(0f)
          → MobaSkillTriggering.Publish(Events.CastStart/Complete/Fail/Interrupt, ctx)
(每帧) MobaSkillPipelineStepSystem → SkillRunnerRegistry.Step(actorId) → runner.Step(dt)
  → SkillRulePlanPhase.OnInstantExecute
    → MobaTriggerPlanExecutor.ExecuteRulePlan(triggerId, context)
      → TriggerPlanJsonDatabase.TryGetPlanByTriggerId → 执行 TriggerPlan<object>
        → 各 PlanActionModule
```

## 关键文件（技能）

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Cast/SkillCastCoordinator.cs` — 施放入口
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Cast/SkillRunnerRegistry.cs` — 按 actorId 持有 `SkillPipelineRunner`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/SkillPipelineRunner.cs` — `Start / Step / CancelAll / CancelBySlot / CancelBySkillId`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Pipeline/SkillPipelineContext.cs` — **内嵌 `SkillCastRequest` readonly struct**（line 354）
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Phases/SkillRulePlanPhase.cs` — 调 `MobaTriggerPlanExecutor.ExecuteRulePlan`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Effects/MobaTriggerPlanExecutor.cs` — Plan 桥（`TriggerPlanJsonDatabase` + 第二套 IEventBus）
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Events/MobaSkillTriggering.cs` — 事件名常量
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Events/MobaSkillTriggerArgs.cs` — arg 键常量（"skill.id" / "caster.actorId" / "target.actorId"）

## Plan 动词清单（moba demo）

位置：`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/`

- **Skill**：`AddBuff` / `GiveDamage` / `TakeDamage` / `ShootProjectile` / `SpawnArea` / `SpawnSummon` / `StartCooldown` / `ConsumeResource` / `AddShield` / `RemoveShield` / `RemoveBuff` / `RemoveArea` / `RemoveSummon` / `CancelSkill`
- **Gameplay**：`SetGameplayVar` / `AddGameplayVar` / `EndGame`
- **Presentation**：`PlayPresentation` / `Emit`
- **Motion**：`Blink`
- **Debug**：`DebugLog`

每个 PlanAction = Args + Schema + PlanActionModule，通过 `[AutoPlanAction]` 特性自动注册到 `ActionSchemaRegistry`。统一执行网关：`MobaTriggerExecutionGateway` + `MobaTriggerPlanSubscriptionService`（`com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/`）。

## BUFF 生命周期调用链

```
外部请求 → MobaBuffService.ApplyBuffImmediate / RemoveBuffImmediate(s)
  → EnqueueApply / EnqueueRemove（入 _pending 命令队列）
  → DrainPending(maxCommands)
    → ExecuteApply → BuffLifecycleExecutor.Apply → BuffApplyFlow.Apply
         → 配置/标签门禁、叠层策略(BuffStackingPolicyApplier)、上下文创建、
           持续行为绑定(BuffContinuousBindingService)
         → BuffLifecycleNotifier → BuffEventPublisher.PublishApplyOrRefresh("buff.apply")  // 第二套 EventBus
         → BuffStageEffectExecutor（经 MobaTriggerExecutionGateway 执行 OnApply 效果 Plan）
    → ExecuteRemove → BuffLifecycleExecutor.Remove/EndRuntime → BuffEndFlow
         → BuffEventPublisher.PublishRemove("buff.remove", reason)
每帧：MobaBuffLifecycleReconcileSystem → MobaBuffService.ReconcileActorBuffLifecycles
  → 同步 Continuous、检查标签移除、过期清理
  → interval: BuffContinuousIntervalHandler → BuffEventPublisher.PublishInterval("buff.interval")
```

## 关键文件（BUFF）

### 入口与服务（`Runtime/Application/Services/Buffs/`）

- `MobaBuffService.cs` — `[WorldService]` 入口（`ApplyBuffImmediate / RemoveBuffImmediate / RemoveBuffsImmediate / RemoveBuffsWithTagImmediate / ReconcileActorBuffLifecycles / DrainPending`）
- `BuffEventPublisher.cs` — 向第二套 EventBus 派发 `buff.apply/remove/interval/tick/...`
- `Lifecycle/BuffLifecycleExecutor.cs` — Apply/Remove 执行器
- `Lifecycle/BuffApplyFlow.cs` / `BuffEndFlow.cs`
- `Core/BuffRepository.cs` / `BuffContextRegistry.cs` / `BuffRuntimeContexts.cs` / `BuffStackingPolicyApplier.cs`
- `Runtime/BuffContinuousRuntime.cs` / `BuffContinuousIntervalHandler.cs` / `BuffContinuousBindingService.cs`
- `Triggering/MobaBuffTriggering.cs` — 纯事件名常量类（Events / Stages / Prefixes）

### Systems（`Runtime/Application/Systems/Buffs/`）

- `MobaBuffCommandDrainSystem.cs` — 每帧驱动 `MobaBuffService.DrainPending`
- `MobaBuffLifecycleReconcileSystem.cs` — 每帧驱动 `ReconcileActorBuffLifecycles`（过期/标签/tick）

**注意**：旧 skill 提到的 `MobaBuffApplySystem / MobaBuffTickSystem / MobaBuffRemoveSystem` **已全部删除**。替代为 `MobaBuffService` + `BuffLifecycleExecutor` + `BuffEventPublisher` + 两个 Reconcile System。

## BUFF 事件清单（MobaBuffTriggering.Events）

- `Apply` = "buff.apply"
- `Remove` = "buff.remove"
- `Interval` = "buff.interval"
- `Tick` = "buff.tick"
- `Stack` / `Refresh` / `End` / `Added` / `Removed` / `StackChanged` / `EffectTick`

事件以强类型 `BuffEventArgs` 经第二套 EventBus 派发，同时发一份 `EventKey<object>` 兼容弱类型订阅。每个事件还派生 `<eventId>.<effectId>` 形式的派生事件。

## 被动技能

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Skill/MobaPassiveSkillTriggerRegisterSystem.cs` — Entitas ReactiveSystem（`OnEntityChanged → SyncActorPassives`）
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Passive/MobaPassiveSkillLifecycleService.cs` — 实际生命周期（Subscribe/Unsubscribe）
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Domain/Components/` — `SkillLoadoutComponent` / `PassiveSkillTriggerListenersComponent`

### 被动调用链

```
SkillLoadoutComponent 变更
  → MobaPassiveSkillTriggerRegisterSystem.OnEntityChanged (Entitas Reactive)
    → MobaPassiveSkillLifecycleService.SyncActorPassives(entity, frame)
      → 按 PassiveSkillRuntime.TriggerIds 在第二套 TriggerRunner 上 Subscribe/Unsubscribe
      → listener 挂在 PassiveSkillTriggerListenersComponent
事件到来 → 第二套 TriggerRunner 派发
  → AllowExternal 过滤在订阅/执行层处理（配置在 Source/Plan DTO）
```

## 排查要点

- 旧 skill 提到的 `SkillExecutor.cs` **已删除** → 改查 `SkillCastCoordinator` + `SkillPipelineRunner`
- 旧 skill 提到的 `evt.Args["caster.actorId"]` 字典访问是**第一套**形式 → 第二套改为属性访问（`args.CasterActorId`）
- 旧 skill 提到的 `MobaBuffApplySystem/Tick/Remove` 三件套 **已删除**
- `AllowExternal` 不在 `TriggerDef` 上 → 在 Source/Plan DTO 层
