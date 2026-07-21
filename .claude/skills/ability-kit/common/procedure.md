# Procedure (how to work on a skill-related task)

## 排查技能/触发器/BUFF 类问题的标准流程

### 1. 确认用的是哪套 EventBus（**最关键**）

- 若改 moba 业务（技能/被动/BUFF/伤害/投射/区域）→ 第二套 `AbilityKit.Triggering.Eventing.IEventBus`
- 若改 ability 包 Effect 层 → 第一套 `AbilityKit.Ability.Triggering.IEventBus`
- 不确定时 grep 代码里 `using AbilityKit.*.Triggering` 与 `IEventBus` 注入来源
- 详见 [triggering_engines.md](../triggering/two_engines.md)

### 2. 确认事件源

- moba 技能事件：`MobaSkillTriggering.Publish(eventId, ctx, failReason)` → 转发到第二套 EventBus
- BUFF 事件：`BuffEventPublisher.PublishApplyOrRefresh` / `PublishRemove` / `PublishInterval`（事件名常量在 `MobaBuffTriggering.Events`）
- 伤害事件：`DamagePipelineService` 的 9 个阶段（damage.attack.created / before_calc / calc.begin / after_base / after_mitigate / after_shield / final / apply.before / apply.after）

### 3. 确认 payload 与 args

- 第二套用强类型 `TArgs`（如 `BuffEventArgs` / `SkillCastContext` / `DamageResult`）
- 旧 skill 提到的 `evt.Args["caster.actorId"]` 等字典访问是**第一套**的形式，第二套改为属性访问（如 `args.CasterActorId`）
- arg 键常量参考 `MobaSkillTriggerArgs`（"skill.id" / "caster.actorId" / "target.actorId"）

### 4. 确认订阅是否建立且生命周期正确

- 被动：`MobaPassiveSkillTriggerRegisterSystem.OnEntityChanged` → `MobaPassiveSkillLifecycleService.SyncActorPassives(entity, frame)` → 在第二套 TriggerRunner 上 Subscribe/Unsubscribe
- `AllowExternal` 字段在 Source/Plan DTO 层（不在 `TriggerDef`），订阅侧据此决定是否注册外部事件
- 反注册必须发生在 entity destroy 或 system 的 `WorldSystemBase.OnTearDown()`（**不要在 `OnCleanup()` 反注册**）

### 5. 确认 trigger 执行路径（第二套）

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

### 6. 确认技能施放路径

```
SkillCastCoordinator.TryCastSkill / CastSkill
  → CastSkillInternal
    → _preparation.Prepare(SkillCastPreparationInput)
    → StartPreparedCast
      → _runnerRegistry.GetOrCreate(actorId)
      → runner.Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in req, ctx, out failReason, in policy)
        → StartPreCast → (完成则) StartCast
          → new SkillPipelineContext().Initialize(instance, req, triggerContext)
          → pipeline.Start(castConfig, context) → run.Tick(0f)
          → MobaSkillTriggering.Publish(Events.CastStart/Complete/Fail/Interrupt, ctx)
(每帧) MobaSkillPipelineStepSystem → SkillRunnerRegistry.Step(actorId) → runner.Step(dt)
  → SkillRulePlanPhase.OnInstantExecute
    → MobaTriggerPlanExecutor.ExecuteRulePlan(triggerId, context)
      → TriggerPlanJsonDatabase.TryGetPlanByTriggerId → 执行 TriggerPlan<object>
        → 各 PlanActionModule（AddBuff/TakeDamage/GiveDamage/ShootProjectile/SpawnArea/SpawnSummon/StartCooldown/ConsumeResource/AddShield/CancelSkill/Blink/...）
```

### 7. 确认 BUFF 执行路径

```
外部请求 → MobaBuffService.ApplyBuffImmediate / RemoveBuffImmediate(s)
  → EnqueueApply / EnqueueRemove（入 _pending 命令队列）
  → DrainPending(maxCommands)
    → ExecuteApply → BuffLifecycleExecutor.Apply → BuffApplyFlow.Apply
         → 配置/标签门禁、叠层策略(BuffStackingPolicyApplier)、上下文创建、持续行为绑定(BuffContinuousBindingService)
         → BuffLifecycleNotifier → BuffEventPublisher.PublishApplyOrRefresh("buff.apply")   // 第二套 EventBus
         → BuffStageEffectExecutor（经 MobaTriggerExecutionGateway 执行 OnApply 效果 Plan）
    → ExecuteRemove → BuffLifecycleExecutor.Remove/EndRuntime → BuffEndFlow
         → BuffEventPublisher.PublishRemove("buff.remove", reason)
每帧：MobaBuffLifecycleReconcileSystem → MobaBuffService.ReconcileActorBuffLifecycles
  → 同步 Continuous、检查标签移除、过期清理
  → interval: BuffContinuousIntervalHandler → BuffEventPublisher.PublishInterval("buff.interval")
```

### 8. 性能与池化检查

- 第一套 args：`PooledTriggerArgs.Rent() + finally Dispose()`（自动归还池）
- 第二套 args：强类型 struct/class，无需手动池化
- 高频路径是否引入 `new List/Dictionary`、LINQ、闭包？
- Runtime 热路径禁用 LINQ/反射；必要分配需注释说明（如 `TriggerRunner.Dispatch` 的 handler 快照）

### 9. asmdef 检查

- 缺类型 → 先看 asmdef references 是否显式列出（不传递）
- 引用 `AbilityKit.Ability` 不会自动得到 `Combat.Damage / Combat.EntityManager / Combat.Targeting`，必须逐条声明
